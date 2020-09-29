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
using Avontus.Rental.Library.Utility;
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
    public class InventoryTransBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper;
        string initializationMode;

        public InventoryTransBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all transfers and adjustments (will call 'InventoryTrans' as a group)
            //      will loop through these and compare VersionStamp against appropriate record in Versions table *****
            MovementCollection all_inventory_trans = MovementCollection.GetMovementCollection(MovementType.TransferNewToRent);
            //TODO: ADH 9/23/2020 - Identify if we need to consider consumable adjustments or if those aren't a thing
            StockedProductAdjustmentCollection all_adjustments = StockedProductAdjustmentCollection.GetStockedProductAdjustmentCollection(ProductType.Product);
            VersionStampList myStockedProductVersions = VersionStampList.GetVersionList(VersionStampList.DataObjectName.StockedProduct);
            
            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            //***** Create Dictionaries to optimize lookups/version compares *****
            Dictionary<string, Movement> myTransfersDictionary = new Dictionary<string, Movement>();
            Dictionary<Guid, StockedProductAdjustment> myAdjustmentsDictionary = new Dictionary<Guid, StockedProductAdjustment>();
            Dictionary<Guid, byte[]> myVersionsDictionary = new Dictionary<Guid, byte[]>();
            foreach (var versionListItem in myStockedProductVersions)
            {
                myVersionsDictionary.Add(versionListItem.Key, versionListItem.Value);
            }

            //***** Loop through Inventory Transfers *****
            foreach (Movement myTransfer in all_inventory_trans)
            {
                string myTransferNumber = myTransfer.MovementNumber;
                string timestampVersion = "0x" + String.Join("", myTransfer.VersionStamp.Select(b => Convert.ToString(b, 16)));

                //***** Add record to data table to be written to Version table in SQL *****
                dt = MySqlHelper.CreateVersionDataRow(dt, "InventoryTrans", myTransferNumber, timestampVersion.ToString());

                //***** Build Dictionary *****
                if (!myTransfersDictionary.ContainsKey(myTransferNumber))
                {
                    myTransfersDictionary.Add(myTransferNumber, myTransfer);
                }
            }

            //***** Loop through all Adjustments *****
            foreach (StockedProductAdjustment myAdjustment in all_adjustments)
            {
                Guid myAdjustmentID = myAdjustment.StockedProductID;

                if (myVersionsDictionary.ContainsKey(myAdjustmentID))
                {
                    string myAdjustmentNumber = myAdjustmentID.ToString();
                    string timestampVersion = "0x" + String.Join("", myVersionsDictionary[myAdjustmentID].Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "Adjustment", myAdjustmentNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myAdjustmentsDictionary.ContainsKey(myAdjustmentID))
                    {
                        myAdjustmentsDictionary.Add(myAdjustmentID, myAdjustment);
                    }
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);

            //***** If in Initialization Mode bypass Data integrations other than Version Controll *****
            if (initializationMode != "1")
            {
                if (myChangedRecords.Rows.Count > 0)
                {
                    InventoryTransRootClass myInventoryTransactions = new InventoryTransRootClass();

                    //***** Create Audit Log and XRef table structures *****            
                    DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        InventoryTransData myInventoryTransData = new InventoryTransData();

                        if (myRow["Entity"].ToString() == "InventoryTrans")
                        {
                            //***** Initalize fields and classes to be used to build data profile *****
                            string myInventoryTransID = myRow["QuantifyID"].ToString();
                            Movement myTransfer = myTransfersDictionary[myInventoryTransID];
                            MovementProductList myInventoryTransProducts = MovementProductList.GetMovementProductList(myTransfer.MovementID);

                            //***** Build header data profile *****              
                            myInventoryTransData.inventory_trans_id = myInventoryTransID;
                            myInventoryTransData.transaction_type = "M";  // M = Material Transfer in WebApps
                            myInventoryTransData.from_warehouse = ((int)Warehouse.New).ToString();
                            myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();

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
                        }
                        else if (myRow["Entity"].ToString() == "Adjustment")
                        {
                            //TODO: ADH 9/28/2020 - Implement Adjustments data population
                            //***** Initalize fields and classes to be used to build data profile *****
                            Guid myAdjustmentID = Guid.Parse(myRow["QuantifyID"].ToString());
                            string myAdjustmentNumber = myAdjustmentID.ToString();
                            //string myAdjustmentDate = DateTime.Now.ToString();
                            StockedProductAdjustment myAdjustment = myAdjustmentsDictionary[myAdjustmentID];

                            //***** Build data profile - will be separate adjustment transactions sent across for each item *****
                            //TODO: ADH 9/29/2020 - BUSINESS DECISION: What should we use for our inventory transaction id, since we won't have a Movement Number for these? 
                            // Should we just let WebApps increment, since now analogue in Quantify?
                            //myInventoryTransData.inventory_trans_id = myAdjustmentNumber;
                            myInventoryTransData.transaction_type = "A";  // A = Adjustment in WebApps
                            InventoryTransLine myTransLine = new InventoryTransLine();
                            myTransLine.part_number = myAdjustment.PartNumber;
                            myTransLine.comment = myAdjustment.Comment;

                            //***** Evaluate and assign from and to warehouses and quantities based on where quantity was previously vs. where it is now
                            //TODO: ADH 9/29/2020 - Need to verify the below does what is intended, and if this is best way to evaluate per business
                            if (myAdjustment.QtyNewOriginal != null)
                            {
                                myInventoryTransData.from_warehouse = ((int)Warehouse.New).ToString();
                                myTransLine.quantity = myAdjustment.QtyNewOriginal.ToString();
                            }
                            else if (myAdjustment.QtyForRentOriginal != null)
                            {
                                myInventoryTransData.from_warehouse = ((int)Warehouse.Available).ToString();
                                myTransLine.quantity = myAdjustment.QtyForRentOriginal.ToString();
                            }
                            else
                            {
                                myProcessStatus = "E1";
                                myErrorText = "Stocked Product does not have any initial new or available quantities - unable to integrate.";
                            }

                            if (myAdjustment.QuantityNew != null)
                            {
                                myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                                myTransLine.received_quantity = myAdjustment.QuantityNew.ToString();
                            }
                            else if (myAdjustment.QuantityForRent != null)
                            {
                                myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                                myTransLine.received_quantity = myAdjustment.QuantityForRent.ToString();
                            }
                            else
                            {
                                myProcessStatus = "E1";
                                myErrorText = "Stocked Product does not have any current new or available quantities - unable to integrate.";
                            }
                            myInventoryTransData.Lines.Add(myTransLine);
                        }

                        //***** Package as class, serialize to JSON and write to audit log table *****
                        myInventoryTransactions.entity = "InventoryTrans";
                        myInventoryTransactions.InventoryTrans = myInventoryTransData;
                        string myJsonObject = JsonConvert.SerializeObject(myInventoryTransactions);

                        //***** Create audit log datarow ******                 
                        auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "InventoryTrans", myInventoryTransData.inventory_trans_id, myJsonObject, "", myProcessStatus, myErrorText);
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
