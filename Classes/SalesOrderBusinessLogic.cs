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

            //***** Also need to include shipments that include out of service return orders (will call Returns) *****
            //TODO: ADH 9/24/2020 - Change this to be maybe a few days back only after we run initial integration
            DateTime startDate = DateTime.Now.AddDays(-14);
            DateTime endDate = DateTime.Now;
            ShipmentList all_returns_list = ShipmentList.GetShipmentList(startDate, endDate);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            //***** Create Dictionaries for Sales and Returns *****
            Dictionary<string, Movement> mySalesDictionary = new Dictionary<string, Movement>();
            Dictionary<string, Shipment> myReturnsDictionary = new Dictionary<string, Shipment>();

            foreach (Movement mySale in all_sales)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
                if (
                    mySale.TypeOfMovement == MovementType.SellNew ||
                    mySale.TypeOfMovement == MovementType.SellForRent ||
                    mySale.TypeOfMovement == MovementType.SellConsumables ||
                    mySale.TypeOfMovement == MovementType.SellDamaged
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

            foreach (ShipmentListItem myReturnListItem in all_returns_list)
            {
                //***** Evaluate if shipment has Out of Service products - this will determine if we need to integrate a return order or not *****
                if (myReturnListItem.ShipmentType == ShipmentType.Return)
                {
                    Shipment myReturn = Shipment.GetShipment(myReturnListItem.ShipmentID, false, false, false);
                    string myReturnNumber = myReturn.ShipmentNumber;
                    string timestampVersion = "0x" + String.Join("", myReturn.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "ReturnOrder", myReturnNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myReturnsDictionary.ContainsKey(myReturnNumber))
                    {
                        myReturnsDictionary.Add(myReturnNumber, myReturn);
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
                            mySalesOrderData.customer_number = myCustomer.AccountingID;
                            mySalesOrderData.reference_number = mySale.BusinessPartnerNumber;

                            //***** Evaluate jobsite and confirm one has been selected. If one hasn't, log it as error *****
                            if (mySale.JobSite != null)
                            {
                                mySalesOrderData.job_number = mySale.JobSite.Number;
                                mySalesOrderData.branch_office = mySale.JobSite.ParentBranchOrLaydown.Number;
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
                                mySalesOrderLine.part_number = saleProductListItem.PartNumber;
                                mySalesOrderLine.quantity = saleProductListItem.Quantity.ToString();
                                mySalesOrderLine.price_ea = saleProductListItem.SellPrice.ToString();
                                mySalesOrderLine.unit_of_measure = myProduct.UnitOfMeasureName;
                                mySalesOrderData.Lines.Add(mySalesOrderLine);
                            }
                        }

                        //***** Process Return Orders *****
                        else if (myRow["Entity"].ToString() == "ReturnOrder")
                        {
                            //***** Initalize fields and classes to be used to build data profile *****
                            string myReturnID = myRow["QuantifyID"].ToString();
                            Shipment myReturn = myReturnsDictionary[myReturnID];
                            BusinessPartner myCustomer = BusinessPartner.GetBusinessPartner(myReturn.FromStockingLocation.BusinessPartnerID);
                            ShipmentProductList myReturnProducts = ShipmentProductList.GetShipmentProductList(myReturn.ShipmentID, ShipmentStatusType.Completed);

                            //***** Build header data profile *****
                            mySalesOrderData.transaction_number = myReturn.ShipmentNumber;
                            mySalesOrderData.customer_number = myCustomer.PartnerNumber;
                            //mySalesOrderData.reference_number = mySale.BusinessPartnerNumber;
                            mySalesOrderData.branch_office = myReturn.FromStockingLocation.ParentBranchOrLaydown.Number;
                            mySalesOrderData.job_number = myReturn.FromStockingLocation.Number;
                            //TODO: ADH 9/23/20 - Identify if this is appropriate warehouse: seems like only option is to return available (since at that point it's used?)
                            mySalesOrderData.from_warehouse = ((int)Warehouse.Available).ToString();

                            //***** Build line item data profile *****
                            foreach (ShipmentProductListItem returnProductListItem in myReturnProducts)
                            {
                                Product myProduct = Product.GetProduct(returnProductListItem.BaseProductID);
                                SalesOrderLine mySalesOrderLine = new SalesOrderLine();
                                mySalesOrderLine.part_number = returnProductListItem.PartNumber;
                                mySalesOrderLine.quantity = returnProductListItem.OutOfServiceQuantity.ToString();
                                //TODO: ADH 9/23/20 - Confirm this is actually the sell price
                                mySalesOrderLine.price_ea = returnProductListItem.Sell.ToString();
                                mySalesOrderLine.unit_of_measure = myProduct.UnitOfMeasureName;
                                mySalesOrderData.Lines.Add(mySalesOrderLine);
                            }
                        }
                        //***** Package as class, serialize to JSON and write to audit log table *****
                        mySalesOrders.entity = "SalesOrder";
                        mySalesOrders.SalesOrder = mySalesOrderData;
                        string myJsonObject = JsonConvert.SerializeObject(mySalesOrders);

                        //***** Create audit log datarow ******                 
                        auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "SalesOrder", mySalesOrderData.transaction_number, myJsonObject, "", myProcessStatus, myErrorText);
                    }

                    //***** Create audit log record for Boomi to go pick up *****
                    DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

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
