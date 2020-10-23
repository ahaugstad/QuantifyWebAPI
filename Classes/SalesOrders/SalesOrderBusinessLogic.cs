// System References
using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Mail;
using System.Web.Http;
using System.Data.SqlClient;
using System.Text;
using System.Web.Management;
using System.Drawing;
using System.Threading.Tasks;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Utility;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;
using Avontus.Rental.Library.Logging;

// Internal Class references
using QuantifyWebAPI.Classes;

// Other References
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;


namespace QuantifyWebAPI.Controllers
{
    public class SalesOrderBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        SharedHelper mySharedHelper = new SharedHelper();
        QuantifyHelper QuantHelper;
        string initializationMode;

        public SalesOrderBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all sales - will loop through this and compare VersionStamp against appropriate record in Versions table *****
            MovementCollection all_sales = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Also need to include shipments that include out of service return orders and consumable sales (will jointly call Shipment Sales) *****
            //TODO: ADH 9/24/2020 - Change this to be maybe a few days back only after we run initial integration
            DateTime startDate = DateTime.Now.AddDays(-14);
            DateTime endDate = DateTime.Now;
            ShipmentList all_shipment_sales_list = ShipmentList.GetShipmentList(startDate, endDate);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            //***** Create Dictionaries for Sales and Shipment Sales *****
            Dictionary<string, Movement> mySalesDictionary = new Dictionary<string, Movement>();
            Dictionary<string, Shipment> myShipmentSalesDictionary = new Dictionary<string, Shipment>();

            foreach (Movement mySale in all_sales)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
                if (
                    mySale.TypeOfMovement == MovementType.SellNew ||
                    mySale.TypeOfMovement == MovementType.SellForRent ||
                    mySale.TypeOfMovement == MovementType.SellConsumables
                    )
                {
                    string mySaleNumber = mySale.MovementNumber;
                    string timestampVersion = "0x" + String.Join("", mySale.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "SalesOrder", mySaleNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!mySalesDictionary.ContainsKey(mySaleNumber))
                    {
                        mySalesDictionary.Add(mySaleNumber, mySale);
                    }
                }
            }

            foreach (ShipmentListItem myShipmentSaleListItem in all_shipment_sales_list)
            {
                //***** Exclude shipment types of Transfers for now - only consider Returns and Deliveries *****
                if (myShipmentSaleListItem.ShipmentType == ShipmentType.Return || myShipmentSaleListItem.ShipmentType == ShipmentType.Delivery)
                {
                    Shipment myShipmentSale = Shipment.GetShipment(myShipmentSaleListItem.ShipmentID, false, false, false);
                    string myShipmentSaleNumber = myShipmentSale.ShipmentNumber;
                    string timestampVersion = "0x" + String.Join("", myShipmentSale.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "ShipmentSalesOrder", myShipmentSaleNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myShipmentSalesDictionary.ContainsKey(myShipmentSaleNumber))
                    {
                        myShipmentSalesDictionary.Add(myShipmentSaleNumber, myShipmentSale);
                    }
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);

            //***** If in Initialization Mode bypass Data integrations other than Version Controll *****
            if (initializationMode != "1")
            {
                //***** Loop through changed records and write to log table to be processed by Boomi *****
                if (myChangedRecords.Rows.Count > 0)
                {
                    //***** Initialize activity log variables and data model class *****
                    DateTime myStartDate = DateTime.Now;
                    int processedRecordCount = 0;
                    SalesOrderRootClass mySalesOrders = new SalesOrderRootClass();

                    //***** Create Audit Log and XRef table structures *****            
                    DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        SalesOrderData mySalesOrderData = new SalesOrderData();

                        //***** Process Sales Orders *****
                        if (myRow["Entity"].ToString() == "SalesOrder")
                        {
                            //***** Initalize fields and classes to be used to build data profile *****
                            string mySalesID = myRow["QuantifyID"].ToString();
                            Movement mySale = mySalesDictionary[mySalesID];
                            BusinessPartner myCustomer = BusinessPartner.GetBusinessPartnerByNumber(mySale.MovementBusinessPartnerNumber);
                            MovementProductList mySaleProducts = MovementProductList.GetMovementProductList(mySale.MovementID);

                            //***** Build header data profile *****
                            mySalesOrderData.transaction_number = mySale.MovementNumber;
                            mySalesOrderData.ship_date = mySale.MovementDate;
                            mySalesOrderData.transaction_date = mySale.CreateDate;
                            //TODO: ADH 9/24/2020 - Still need to find where Entered By field is in Quantify, if anywhere
                            mySalesOrderData.entered_by = "QuantifyInt";

                            //***** Evaluate jobsite and confirm one has been selected. If one hasn't, log it as error *****
                            if (mySale.JobSite != null)
                            {
                                mySalesOrderData.job_number = mySale.JobSite.Number;
                            }
                            else
                            {
                                myErrorText = "Jobsite is blank. Please provide a Jobsite to integrate this order.";
                                myProcessStatus = "E1";
                            }

                            //***** Assign warehouse based on type of movement *****
                            switch (mySale.TypeOfMovement)
                            {
                                case MovementType.SellNew:
                                    mySalesOrderData.from_warehouse = ((int)Warehouse.New).ToString();
                                    break;
                                case MovementType.SellForRent:
                                    mySalesOrderData.from_warehouse = ((int)Warehouse.Available).ToString();
                                    break;
                                case MovementType.SellConsumables:
                                    mySalesOrderData.from_warehouse = ((int)Warehouse.Consumable).ToString();
                                    break;
                            }

                            //***** Build line item data profile *****
                            foreach (MovementProductListItem saleProductListItem in mySaleProducts)
                            {
                                Product myProduct = Product.GetProduct(saleProductListItem.BaseProductID);
                                SalesOrderLine mySalesOrderLine = new SalesOrderLine();

                                //***** Get WebApps Product ID from Products XRef and assign as Product ID if it exists *****
                                mySalesOrderLine.part_number = mySharedHelper.EvaluateProductXRef(myProduct.ProductID, saleProductListItem.PartNumber, connectionString);
                                mySalesOrderLine.quantity = saleProductListItem.Quantity.ToString();
                                mySalesOrderLine.price_ea = saleProductListItem.AverageCost.ToString();
                                mySalesOrderData.Lines.Add(mySalesOrderLine);
                            }
                        }

                        //***** Process Shipment Sales Orders (Sales Orders generated through Rental Deliveries and Rental Returns) *****
                        else if (myRow["Entity"].ToString() == "ShipmentSalesOrder")
                        {
                            //***** Initalize fields and classes to be used to build data profile *****
                            string myShipmentSalesID = myRow["QuantifyID"].ToString();
                            Shipment myShipmentSale = myShipmentSalesDictionary[myShipmentSalesID];
                            BusinessPartner myCustomer = BusinessPartner.GetBusinessPartner(myShipmentSale.FromStockingLocation.BusinessPartnerID);
                            ShipmentProductList myShipmentSaleProducts = ShipmentProductList.GetShipmentProductList(myShipmentSale.ShipmentID, ShipmentStatusType.Completed);

                            //***** Build header data profile *****
                            mySalesOrderData.transaction_number = myShipmentSale.ShipmentNumber.Substring(4);   //Trim off DEL-, TRN-, RET- for shipment numbers
                            mySalesOrderData.ship_date = myShipmentSale.ActualShipDate;
                            mySalesOrderData.transaction_date = myShipmentSale.CreateDate;
                            //TODO: ADH 9/24/2020 - Still need to find where Entered By field is in Quantify, if anywhere
                            mySalesOrderData.entered_by = "QuantifyInt";

                            //***** Evaluate Shipment Type to determine Jobsite and Warehouse to send through *****
                            if (myShipmentSale.ShipmentType == ShipmentType.Return)
                            {
                                mySalesOrderData.job_number = myShipmentSale.FromStockingLocation.Number;
                                mySalesOrderData.from_warehouse = ((int)Warehouse.Available).ToString();
                            }
                            else if (myShipmentSale.ShipmentType == ShipmentType.Delivery)
                            {
                                mySalesOrderData.job_number = myShipmentSale.ToStockingLocation.Number;
                                mySalesOrderData.from_warehouse = ((int)Warehouse.Consumable).ToString();
                            }
                             
                            //***** Build line item data profile *****
                            foreach (ShipmentProductListItem shipmentSaleProductListItem in myShipmentSaleProducts)
                            {
                                if (shipmentSaleProductListItem.OutOfServiceQuantity != null || shipmentSaleProductListItem.ProductType == ProductType.Consumable)
                                {
                                    Product myProduct = Product.GetProduct(shipmentSaleProductListItem.BaseProductID);
                                    SalesOrderLine mySalesOrderLine = new SalesOrderLine();

                                    //***** Get WebApps Product ID from Products XRef and assign as Product ID if it exists *****
                                    mySalesOrderLine.part_number = mySharedHelper.EvaluateProductXRef(myProduct.ProductID, shipmentSaleProductListItem.PartNumber, connectionString);
                                    mySalesOrderLine.price_ea = shipmentSaleProductListItem.AverageCost.ToString();

                                    //***** If Shipment Return, only consider Out of Service quantities; if Shipment Delivery, only consider Sell quantities *****
                                    if (myShipmentSale.ShipmentType == ShipmentType.Return)
                                    {
                                        mySalesOrderLine.quantity = shipmentSaleProductListItem.OutOfServiceQuantity.ToString();
                                        mySalesOrderData.Lines.Add(mySalesOrderLine);
                                    }
                                    else if (myShipmentSale.ShipmentType == ShipmentType.Delivery)
                                    {
                                        mySalesOrderLine.quantity = shipmentSaleProductListItem.SentQuantity.ToString();
                                        mySalesOrderData.Lines.Add(mySalesOrderLine);
                                    }
                                }
                            }
                        }
                        //***** Only create an adjustment package to process if it actually produces results *****
                        if (mySalesOrderData.Lines.Count > 0)
                        {
                            //***** Package as class, serialize to JSON and write to audit log table *****
                            mySalesOrders.entity = "SalesOrder";
                            mySalesOrders.SalesOrder = mySalesOrderData;
                            string myJsonObject = JsonConvert.SerializeObject(mySalesOrders);

                            //***** Create audit log datarow ******                 
                            auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "SalesOrder", mySalesOrderData.transaction_number, myJsonObject, "", myProcessStatus, myErrorText);
                            processedRecordCount++;
                        }
                    }

                    //***** Create audit log record for Boomi to go pick up *****
                    DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

                    //***** Create activity log record for reference *****
                    DataTable myActivityLog = myDAL.InsertClassActivityLog("SalesOrders", "", processedRecordCount, myStartDate, DateTime.Now, connectionString);

                    string result = myReturnResult.Rows[0][0].ToString();
                    if (result.ToLower() == "success")
                    {
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            return success;
        }
    }
}
