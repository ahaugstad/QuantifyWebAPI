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
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;

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

        public SalesOrderBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all sales - will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            MovementCollection all_sales = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Also need to include shipments that include out of service return orders *****
            ShipmentCollection all_shipments = ShipmentCollection.GetShipmentCollection(Guid.Empty);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> mySalesDictionary = new Dictionary<string, Movement>();

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

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                SalesOrderRootClass mySalesOrders = new SalesOrderRootClass();

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    //***** Initalize fields and classes to be used to build data profile *****
                    string mySalesID = myRow["QuantifyID"].ToString();
                    Movement mySale = mySalesDictionary[mySalesID];
                    Order myOrder = Order.GetOrder(mySale.OrderID);
                    BusinessPartner myCustomer = BusinessPartner.GetBusinessPartnerByNumber(mySale.MovementBusinessPartnerNumber);
                    MovementProductList mySaleProducts = MovementProductList.GetMovementProductList(mySale.MovementID);
                    
                    //***** Build header data profile *****
                    SalesOrderData mySalesOrderData = new SalesOrderData();
                    mySalesOrderData.transaction_number = mySale.MovementNumber;
                    mySalesOrderData.customer_number = myCustomer.AccountingID;
                    mySalesOrderData.job_number = mySale.JobSite.Number;
                    mySalesOrderData.reference_number = mySale.BusinessPartnerNumber;
                    mySalesOrderData.branch_office = mySale.JobSite.ParentBranchOrLaydown.Number;

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

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    mySalesOrders.entity = "SalesOrder";
                    mySalesOrders.SalesOrder = mySalesOrderData;
                    string myJsonObject = JsonConvert.SerializeObject(mySalesOrders);

                    //***** Create audit log datarow ******                 
                    auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "SalesOrder", mySalesOrderData.transaction_number, myJsonObject, "", "A");
                }
                //***** Create audit log record for Boomi to go pick up *****
                // REST API URL: http://apimariaasad01.apigroupinc.api:9090/ws/rest/webapps_quantify/api
                DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

                //TODO: ADH 9/4/2020 - Figure out why following line is failing
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
            return success;
        }
    }
}
