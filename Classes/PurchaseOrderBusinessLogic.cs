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
using Avontus.Rental.Library.Logging;

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
        QuantifyHelper QuantHelper;

        public PurchaseOrderBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all purchases - will loop through this and compare VersionStamp against appropriate record in Versions table *****
            MovementCollection all_purchases = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myPurchasesDictionary = new Dictionary<string, Movement>();

            foreach (Movement myPurchase in all_purchases)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
                if (
                    myPurchase.TypeOfMovement == MovementType.BackOrderCancelled ||
                    myPurchase.TypeOfMovement == MovementType.BackOrderCompleted ||
                    myPurchase.TypeOfMovement == MovementType.BackOrderCompletedWithBackOrder ||
                    myPurchase.TypeOfMovement == MovementType.NewBackOrderCancelled ||
                    myPurchase.TypeOfMovement == MovementType.NewBackOrderCompleted ||
                    myPurchase.TypeOfMovement == MovementType.NewBackOrderCompletedWithBackOrder ||
                    myPurchase.TypeOfMovement == MovementType.NewOrderCompleted ||
                    myPurchase.TypeOfMovement == MovementType.NewOrderCompletedWithBackOrder ||
                    myPurchase.TypeOfMovement == MovementType.OrderCompleted ||
                    myPurchase.TypeOfMovement == MovementType.OrderCompletedWithBackOrder ||
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
                    //***** Initialize error tracking fields and data package *****
                    string myErrorText = "";
                    string myProcessStatus = "A";
                    PurchaseOrderData myPurchaseOrderData = new PurchaseOrderData();

                    //***** Initalize fields and classes to be used to build data profile *****
                    string myPurchaseID = myRow["QuantifyID"].ToString();
                    Movement myPurchase = myPurchasesDictionary[myPurchaseID];
                    Order myOrder = Order.GetOrder(myPurchase.OrderID);
                    BusinessPartner myVendor = BusinessPartner.GetBusinessPartnerByNumber(myPurchase.MovementBusinessPartnerNumber);
                    MovementProductList myPurchaseProducts = MovementProductList.GetMovementProductList(myPurchase.MovementID);
                    
                    //***** Build header data profile *****                   
                    myPurchaseOrderData.transaction_number = myPurchase.MovementNumber;
                    myPurchaseOrderData.vendor_number = myVendor.AccountingID;
                    //myPurchaseOrderData.order_number = myPurchase.MovementNumber;

                    //***** Use ReferenceNumber instead of BackOrderNumber for PO Number if we are doing direct purchase of consumables *****
                    //TODO: ADH 9/23/20 - Code duplicate BackOrderNumber and BusinessPartnerNumber check here and flag errors
                    if (myPurchase.TypeOfMovement == MovementType.PurchaseConsumables)
                    {
                        myPurchaseOrderData.order_number = myPurchase.BusinessPartnerNumber;
                    }
                    else
                    {
                        myPurchaseOrderData.order_number = myPurchase.BackOrderNumber;
                    }

                    //***** Assign warehouse based on type of movement *****
                    switch (myPurchase.TypeOfMovement)
                    {
                        //TODO: ADH 9/23/2020 - BUSINESS QUESTION: How do we handle Cancelled backorders?
                        case MovementType.BackOrderCancelled:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        case MovementType.BackOrderCompleted:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        case MovementType.BackOrderCompletedWithBackOrder:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        //TODO: ADH 9/23/2020 - BUSINESS QUESTION: How do we handle Cancelled backorders?
                        case MovementType.NewBackOrderCancelled:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.NewBackOrderCompleted:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.NewBackOrderCompletedWithBackOrder:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.NewOrderCompletedWithBackOrder:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.NewOrderCompleted:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.OrderCompletedWithBackOrder:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        case MovementType.OrderCompleted:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        case MovementType.NewOrdered:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.New).ToString();
                            break;
                        case MovementType.Ordered:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Available).ToString();
                            break;
                        case MovementType.PurchaseConsumables:
                            myPurchaseOrderData.to_warehouse = ((int)Warehouse.Consumable).ToString(); 
                            break;
                    }
                    myPurchaseOrderData.notes = myPurchase.Notes;

                    //TODO: ADH 9/24/20 - BUSINESS DECISION: Identify how we are going to handle this 'entered_by' functionality, since it only gets populated after receiving
                    myPurchaseOrderData.entered_by = "QuantifyInt";
                    //if (myPurchase.MovementNumber == "MOV-0000126")
                    //{
                    //    LogEntryList logEntryList = LogEntryList.GetLogEntryList(myPurchase.MovementID);
                    //    myPurchaseOrderData.entered_by = logEntryList[0].UserName;
                    //}
                    
                    //***** Build line item data profile *****
                    foreach (MovementProductListItem purchaseProductListItem in myPurchaseProducts)
                    {
                        Product myProduct = Product.GetProduct(purchaseProductListItem.BaseProductID);
                        PurchaseOrderLine myPurchaseOrderLine = new PurchaseOrderLine();
                        myPurchaseOrderLine.part_number = purchaseProductListItem.PartNumber;   
                        myPurchaseOrderLine.quantity = purchaseProductListItem.Quantity.ToString();
                        //***** Only set received quantity if we are doing direct purchase of consumables *****
                        if (myPurchase.TypeOfMovement != MovementType.NewOrdered && myPurchase.TypeOfMovement != MovementType.Ordered)
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
                    auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "PurchaseOrder", myPurchaseOrderData.transaction_number, myJsonObject, "", myProcessStatus, myErrorText);
                }

                //***** Create audit log record for Boomi to go pick up *****
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
