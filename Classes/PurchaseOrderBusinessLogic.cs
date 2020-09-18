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
    public class PurchaseOrderBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper = new QuantifyHelper();
        BoomiHelper BoomiHelper = new BoomiHelper();

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all purchases - will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            //MovementCollection consumable_purchases = MovementCollection.GetMovementCollection(MovementType.PurchaseConsumables);
            //MovementCollection new_purchases = MovementCollection.GetMovementCollection(MovementType.NewOrdered);
            //MovementCollection available_purchases = MovementCollection.GetMovementCollection(MovementType.Ordered);

            //var all_purchases = consumable_purchases.Concat(available_purchases.Concat(new_purchases));
            //var all_purchases = new_purchases;

            //TODO: ADH 9/18/2020 - if we can't merge collections, just grab all movements and filter later (and delete all commented lines above)
            MovementCollection all_purchases = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myPurchasesDictionary = new Dictionary<string, Movement>();

            foreach (Movement myPurchase in all_purchases)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
                if (
                    myPurchase.TypeOfMovement == MovementType.PurchaseConsumables ||
                    myPurchase.TypeOfMovement == MovementType.NewOrdered ||
                    myPurchase.TypeOfMovement == MovementType.Ordered
                    )
                {
                    string myPurchaseNumber = myPurchase.MovementNumber;
                    string timestampVersion = "0x" + String.Join("", myPurchase.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "PurchaseOrder", myPurchaseNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myPurchasesDictionary.ContainsKey(myPurchaseNumber))
                    {
                        myPurchasesDictionary.Add(myPurchaseNumber, myPurchase);
                    }
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                PurchaseOrderRootClass myPurchaseOrders = new PurchaseOrderRootClass();

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    //***** Initalize fields and classes to be used to build data profile *****
                    string myPurchaseID = myRow["QuantifyID"].ToString();
                    Movement myPurchase = myPurchasesDictionary[myPurchaseID];
                    Order myOrder = Order.GetOrder(myPurchase.OrderID);
                    BusinessPartner myVendor = BusinessPartner.GetBusinessPartnerByNumber(myPurchase.MovementBusinessPartnerNumber);
                    MovementProductList myPurchaseProducts = MovementProductList.GetMovementProductList(myPurchase.MovementID);
                    
                    //***** Build header data profile *****
                    PurchaseOrderData myPurchaseOrderData = new PurchaseOrderData();                    
                    myPurchaseOrderData.transaction_number = myPurchase.MovementNumber;
                    myPurchaseOrderData.transaction_type = myPurchase.TypeOfMovementText;
                    //***** Use ReferenceNumber instead of BackOrderNumber for PO Number if we are doing direct purchase of consumables *****
                    if (myPurchase.TypeOfMovement == MovementType.PurchaseConsumables)
                    {
                        myPurchaseOrderData.order_number = myPurchase.BusinessPartnerNumber;
                    }
                    else
                    {
                        myPurchaseOrderData.order_number = myPurchase.BackOrderNumber;
                    }     
                    myPurchaseOrderData.vendor_number = myVendor.AccountingID;
                    //TODO: ADH 9/17/2020 - Is there another way to get branch office number without going out to the jobsite? (likely Avontus question)
                    //myPurchaseOrderData.branch_office = myPurchase.JobSite.ParentBranchOrLaydown.Number;
                    myPurchaseOrderData.branch_office = "3005";
                    myPurchaseOrderData.notes = myPurchase.Notes;
                    myPurchaseOrderData.date = myPurchase.MovementDate;
                    
                    //***** Build line item data profile *****
                    foreach (MovementProductListItem purchaseProductListItem in myPurchaseProducts)
                    {
                        Product myProduct = Product.GetProduct(purchaseProductListItem.BaseProductID);
                        PurchaseOrderLine myPurchaseOrderLine = new PurchaseOrderLine();
                        myPurchaseOrderLine.part_number = purchaseProductListItem.PartNumber;            
                        myPurchaseOrderLine.quantity = purchaseProductListItem.Quantity.ToString();
                        //***** Only set received quantity if we are doing direct purchase of consumables *****
                        if (myPurchase.TypeOfMovement == MovementType.PurchaseConsumables)
                        {
                            myPurchaseOrderLine.received_quantity = purchaseProductListItem.ReceivedQuantity.ToString();
                        }
                        myPurchaseOrderLine.cost = purchaseProductListItem.PurchasePrice.ToString();
                        myPurchaseOrderLine.unit_of_measure = myProduct.UnitOfMeasureName;
                        myPurchaseOrderData.Lines.Add(myPurchaseOrderLine);
                    }

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    myPurchaseOrders.entity = "PurchaseOrder";
                    myPurchaseOrders.PurchaseOrder = myPurchaseOrderData;
                    string myJsonObject = JsonConvert.SerializeObject(myPurchaseOrders);

                    //***** Create audit log datarow ******                 
                    auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "PurchaseOrder", myPurchaseOrderData.transaction_number, myJsonObject, "", "A");
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

                //TODO: ADH 9/3/2020 - Ping Boomi to kick off process to start running through queued events
                BoomiHelper.PostBoomiAPI();
            }
            return success;
        }
    }
}
