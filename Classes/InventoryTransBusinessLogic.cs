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
        QuantifyHelper QuantHelper = new QuantifyHelper();
        BoomiHelper BoomiHelper = new BoomiHelper();

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all transfers and adjustments (will call 'InventoryTrans')- will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            //TODO: ADH 9/16/2020 - Identify where inventory adjustments are housed in API/database, then merge with transfers for all movements
            //MovementCollection all_adjustments = MovementCollection.GetMovementCollection(MovementType.?);
            //MovementCollection all_transfers = MovementCollection.GetMovementCollection(MovementType.TransferNewToRent);
            //MovementCollection new_completed_backorder = MovementCollection.GetMovementCollection(MovementType.NewOrderCompletedWithBackOrder);
            //MovementCollection new_completed = MovementCollection.GetMovementCollection(MovementType.NewOrderCompleted);
            //MovementCollection available_completed_backorder = MovementCollection.GetMovementCollection(MovementType.OrderCompletedWithBackOrder);
            //MovementCollection available_completed = MovementCollection.GetMovementCollection(MovementType.OrderCompleted);

            //***** Consolidate movements (transfers and adjustments) and receipts separately, then merge all together *****
            //var all_movements = all_transfers.Concat(all_adjustments);
            //var all_movements = all_transfers;
            //var all_receipts = new_completed_backorder.Concat(new_completed.Concat(available_completed_backorder.Concat(available_completed)));
            //var all_inventory_trans = all_movements.Concat(all_receipts);

            //TODO: ADH 9/18/2020 - if we can't merge collections, just grab all movements and filter later (and delete all commented lines above)
            MovementCollection all_inventory_trans = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myInventoryTransDictionary = new Dictionary<string, Movement>();

            foreach (Movement myInventoryTrans in all_inventory_trans)
            {
                //***** Need to include all types of transactions that result in inventory being received, either partially or fully *****
                //      (i.e. green arrow at left of transaction in list in Quantify turns gray)
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
                    myInventoryTrans.TypeOfMovement == MovementType.OrderCompletedWithBackOrder
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
                    myInventoryTransData.package_type = myInventoryTrans.BusinessPartnerType.ToDescription();
                    switch (myInventoryTrans.TypeOfMovement)
                    {
                        case MovementType.NewOrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = "2";
                            break;
                        case MovementType.NewOrderCompleted:
                            myInventoryTransData.to_warehouse = "2";
                            break;
                        case MovementType.OrderCompletedWithBackOrder:
                            myInventoryTransData.to_warehouse = "3";
                            break;
                        case MovementType.OrderCompleted:
                            myInventoryTransData.to_warehouse = "3";
                            break;
                        case MovementType.PurchaseConsumables:
                            myInventoryTransData.to_warehouse = "4";
                            break;
                        case MovementType.TransferNewToRent:
                            myInventoryTransData.to_warehouse = "3";
                            break;
                        //TODO: ADH - Include adjustments handling when applicable
                    }

                    //***** Build line item data profile *****
                    foreach (MovementProductListItem inventoryTransProductListItem in myInventoryTransProducts)
                    {
                        Product myProduct = Product.GetProduct(inventoryTransProductListItem.BaseProductID);
                        InventoryTransLine myTransLine = new InventoryTransLine();
                        myTransLine.part_number = inventoryTransProductListItem.PartNumber;
                        myTransLine.serial_number = myProduct.SerialNumber;
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

                //TODO: ADH 9/3/2020 - Ping Boomi to kick off process to start running through queued events
                BoomiHelper.PostBoomiAPI();
            }
            return success;
        }
    }
}
