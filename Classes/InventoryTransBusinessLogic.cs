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
    public class InventoryTransBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper;

        public InventoryTransBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all transfers and adjustments (will call 'InventoryTrans')- will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            MovementCollection all_inventory_trans = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myInventoryTransDictionary = new Dictionary<string, Movement>();

            foreach (Movement myInventoryTrans in all_inventory_trans)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
                //TODO: ADH 9/16/2020 - Identify where inventory adjustments are housed in API/database, then merge with below
                if (
                    myInventoryTrans.TypeOfMovement == MovementType.BackOrderCancelled ||
                    myInventoryTrans.TypeOfMovement == MovementType.BackOrderCompleted ||
                    myInventoryTrans.TypeOfMovement == MovementType.BackOrderCompletedWithBackOrder ||
                    myInventoryTrans.TypeOfMovement == MovementType.NewBackOrderCancelled ||
                    myInventoryTrans.TypeOfMovement == MovementType.NewBackOrderCompleted ||
                    myInventoryTrans.TypeOfMovement == MovementType.NewBackOrderCompletedWithBackOrder ||
                    myInventoryTrans.TypeOfMovement == MovementType.NewOrderCompleted ||
                    myInventoryTrans.TypeOfMovement == MovementType.NewOrderCompletedWithBackOrder ||
                    myInventoryTrans.TypeOfMovement == MovementType.OrderCompleted ||
                    myInventoryTrans.TypeOfMovement == MovementType.OrderCompletedWithBackOrder ||
                    myInventoryTrans.TypeOfMovement == MovementType.TransferNewToRent
                    )
                {
                    string myInventoryTransNumber = myInventoryTrans.MovementNumber;
                    string timestampVersion = "0x" + String.Join("", myInventoryTrans.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "InventoryTrans", myInventoryTransNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myInventoryTransDictionary.ContainsKey(myInventoryTransNumber))
                    {
                        myInventoryTransDictionary.Add(myInventoryTransNumber, myInventoryTrans);
                    }
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                InventoryTransRootClass myInventoryTransactions = new InventoryTransRootClass();

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    //***** Initalize fields and classes to be used to build data profile *****
                    string myInventoryTransID = myRow["QuantifyID"].ToString();
                    Movement myInventoryTrans = myInventoryTransDictionary[myInventoryTransID];
                    Order myOrder = Order.GetOrder(myInventoryTrans.OrderID);
                    MovementProductList myInventoryTransProducts = MovementProductList.GetMovementProductList(myInventoryTrans.MovementID);
                    
                    //***** Build header data profile *****
                    InventoryTransData myInventoryTransData = new InventoryTransData();                    
                    myInventoryTransData.inventory_trans_id = myInventoryTransID;
                    myInventoryTransData.transaction_type = myInventoryTrans.TypeOfMovement.ToDescription();
                    //myInventoryTransData.package_type = myInventoryTrans.BusinessPartnerType.ToDescription();
                    switch (myInventoryTrans.TypeOfMovement)
                    {
                        case MovementType.BackOrderCancelled:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            //TODO: ADH 9/21/2020 - Identify if this should be R type in WebApps or something else
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.BackOrderCompleted:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.BackOrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.NewBackOrderCancelled:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                            //TODO: ADH 9/21/2020 - Identify if this should be R type in WebApps or something else
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.NewBackOrderCompleted:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.NewBackOrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.NewOrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.NewOrderCompleted:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.OrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.OrderCompleted:
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            myInventoryTransData.transaction_type = "R";
                            break;
                        case MovementType.TransferNewToRent:
                            myInventoryTransData.from_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                            myInventoryTransData.transaction_type = "M";
                            break;
                            //TODO: ADH - Include adjustments handling when applicable
                            //myInventoryTransData.transaction_type = "A";
                    }

                    //***** Build line item data profile *****
                    foreach (MovementProductListItem inventoryTransProductListItem in myInventoryTransProducts)
                    {
                        Product myProduct = Product.GetProduct(inventoryTransProductListItem.BaseProductID);
                        InventoryTransLine myTransLine = new InventoryTransLine();
                        myTransLine.part_number = inventoryTransProductListItem.PartNumber;
                        //myTransLine.serial_number = myProduct.SerialNumber;
                        myTransLine.quantity = inventoryTransProductListItem.Quantity.ToString();
                        myTransLine.received_quantity = inventoryTransProductListItem.ReceivedQuantity.ToString();
                        myTransLine.comment = inventoryTransProductListItem.Comment;
                        myInventoryTransData.Lines.Add(myTransLine);
                    }

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    myInventoryTransactions.entity = "InventoryTrans";
                    myInventoryTransactions.InventoryTrans = myInventoryTransData;
                    string myJsonObject = JsonConvert.SerializeObject(myInventoryTransactions);

                    //***** Create audit log datarow ******                 
                    auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "InventoryTrans", myInventoryTransData.inventory_trans_id, myJsonObject, "", "A");
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
