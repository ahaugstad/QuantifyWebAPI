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
            //TODO: ADH 10/7/2020 - TEST: Performance of large amounts of adjustments in one batch - if bad, potentially switch to using List
            StockedProductAdjustmentCollection all_adjustments = StockedProductAdjustmentCollection.GetStockedProductAdjustmentCollection(ProductType.All);
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

                    //***** Create Separate Dictionaries for New and Available Adjustments
                    Dictionary<Guid, StockedProductAdjustment> myNewAdjustmentsDictionary = new Dictionary<Guid, StockedProductAdjustment>();
                    Dictionary<Guid, StockedProductAdjustment> myAvailableAdjustmentsDictionary = new Dictionary<Guid, StockedProductAdjustment>();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        InventoryTransData myInventoryTransData = new InventoryTransData();

                        //***** Create Material Transfer Transactions *****
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
                                myTransLine.quantity = inventoryTransProductListItem.Quantity.ToString();
                                myInventoryTransData.Lines.Add(myTransLine);
                            }
                            //***** Package as class, serialize to JSON and write to audit log table *****
                            myInventoryTransactions.entity = "InventoryTrans";
                            myInventoryTransactions.InventoryTrans = myInventoryTransData;
                            string myJsonObject = JsonConvert.SerializeObject(myInventoryTransactions);

                            //***** Create audit log datarow ******                 
                            auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "InventoryTrans", myInventoryTransData.inventory_trans_id, myJsonObject, "", myProcessStatus, myErrorText);

                            //***** Create audit log record for Boomi to go pick up *****
                            DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);
                        }

                        //***** Populate Adjustment Dictionaries to loop through later *****
                        else if (myRow["Entity"].ToString() == "Adjustment")
                        {
                            //***** Initalize fields and classes to be used to build data profile *****
                            Guid myAdjustmentID = Guid.Parse(myRow["QuantifyID"].ToString());
                            StockedProductAdjustment myAdjustment = myAdjustmentsDictionary[myAdjustmentID];

                            //if (myAdjustment.QtyNewOriginal != myAdjustment.QuantityNew)
                            if (myAdjustment.QuantityNew != null)
                            {
                                //***** Build Dictionary *****
                                if (!myNewAdjustmentsDictionary.ContainsKey(myAdjustmentID))
                                {
                                    myNewAdjustmentsDictionary.Add(myAdjustmentID, myAdjustment);
                                }
                            }
                            //else if (myAdjustment.QtyForRentOriginal != myAdjustment.QuantityForRent)
                            if (myAdjustment.QuantityForRent != null)
                            {
                                //***** Build Dictionary *****
                                if (!myAvailableAdjustmentsDictionary.ContainsKey(myAdjustmentID))
                                {
                                    myAvailableAdjustmentsDictionary.Add(myAdjustmentID, myAdjustment);
                                }
                            }
                        }
                    }

                    //***** Create both New and Available Adjustments consolidated transactions *****
                    LogEntryList myStockedProductLogs = LogEntryList.GetLogEntryList(LogEntry.ChildTypes.StockedProduct);
                    CreateAdjustmentTransaction("New", myInventoryTransactions, myNewAdjustmentsDictionary, connectionString, myStockedProductLogs);
                    CreateAdjustmentTransaction("Available", myInventoryTransactions, myAvailableAdjustmentsDictionary, connectionString, myStockedProductLogs);
                }
            }
            return success;
        }

        public void CreateAdjustmentTransaction(string newOrAvailable, InventoryTransRootClass myInventoryTransactions, Dictionary<Guid, StockedProductAdjustment> myAdjustmentsDictionary, string connectionString, LogEntryList myStockedProductLogs)
        {
            //***** Skip if we did not get any adjustments to integrate *****
            if (myAdjustmentsDictionary.Count > 0)
            {
                //TODO: ADH 9/29/2020 - TEST: Need to verify the below does what is intended, and if this is best way to evaluate per business
                //***** Initialize error tracking fields and data package *****
                var myErrorText = "";
                string myProcessStatus = "A";
                var myAdjustmentID = DateTime.Now.ToString() + " | " + newOrAvailable;
                InventoryTransData myInventoryTransData = new InventoryTransData();

                //***** Build header of Available Adjustments data package
                myInventoryTransData.transaction_type = "A";  // A = Adjustment in WebApps
                if (newOrAvailable == "New")
                {
                    myInventoryTransData.to_warehouse = ((int)Warehouse.New).ToString();
                    myInventoryTransData.from_warehouse = ((int)Warehouse.New).ToString();   
                }
                else if (newOrAvailable == "Available")
                {
                    myInventoryTransData.to_warehouse = ((int)Warehouse.Available).ToString();
                    myInventoryTransData.from_warehouse = ((int)Warehouse.Available).ToString();
                }

                foreach (StockedProductAdjustment myAdjustment in myAdjustmentsDictionary.Values)
                {
                    //***** Get list of log entries for adjustment's stocking location (this is needed to obtain previous quantity) *****
                    string[] separatorString = { " - " };

                    //***** Build lines of Available Adjustments data package *****
                    InventoryTransLine myTransLine = new InventoryTransLine();
                    myTransLine.part_number = myAdjustment.PartNumber;
                    if (newOrAvailable == "New")
                    {
                        //TODO: ADH 10/1/2020 - Convert 'item.ParentName' to 'item.Product-ID' when available
                        //***** Get most recent log entry for StockedProduct record for "New Part Changed" log type and create TransLine if exists*****
                        if (myStockedProductLogs.Any(item => item.Name == "New Part Changed" && item.LogDate > DateTime.Now.AddHours(-1) && item.ChildDescription.Split(separatorString, StringSplitOptions.None)[0] == myAdjustment.PartNumber))
                        {
                            var myAdjustmentLog = myStockedProductLogs.First(item => item.Name == "New Part Changed" && item.LogDate > DateTime.Now.AddHours(-1) && item.ChildDescription.Split(separatorString, StringSplitOptions.None)[0] == myAdjustment.PartNumber);
                            myTransLine.quantity = calculateChangedQuantity(myAdjustmentLog).ToString();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (newOrAvailable == "Available")
                    {
                        //TODO: ADH 10/1/2020 - Convert 'item.ParentName' to 'item.Product-ID' when available
                        //***** Get most recent log entry for StockedProduct record for "Available Part Changed" log type and create TransLine if exists *****
                        if (myStockedProductLogs.Any(item => item.Name == "Available Part Changed" && item.LogDate > DateTime.Now.AddHours(-1) && item.ChildDescription.Split(separatorString, StringSplitOptions.None)[0] == myAdjustment.PartNumber))
                        {
                            var myAdjustmentLog = myStockedProductLogs.Last(item => item.Name == "Available Part Changed" && item.LogDate > DateTime.Now.AddHours(-1) && item.ChildDescription.Split(separatorString, StringSplitOptions.None)[0] == myAdjustment.PartNumber);
                            myTransLine.quantity = calculateChangedQuantity(myAdjustmentLog).ToString();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    myInventoryTransData.Lines.Add(myTransLine);
                }

                //***** Package as class, serialize to JSON and write to audit log table *****
                myInventoryTransactions.entity = "InventoryTrans";
                myInventoryTransactions.InventoryTrans = myInventoryTransData;
                string myJsonObject = JsonConvert.SerializeObject(myInventoryTransactions);

                //***** Create audit log datarow ******   
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();
                auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "InventoryTrans", myAdjustmentID, myJsonObject, "", myProcessStatus, myErrorText);

                //***** Create audit log record for Boomi to go pick up *****
                DAL myDAL = new DAL();
                DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);
            }
        }

        public double calculateChangedQuantity(LogEntryListItem myAdjustmentLog)
        {
            //***** Evaluate values and ensure we are converting blanks and nulls to 0s *****
            double myNewValue;
            double myOldValue;
            if (myAdjustmentLog.NewValue == "" || myAdjustmentLog.NewValue == null)
            {
                myNewValue = 0;
            }
            else
            {
                myNewValue = Double.Parse(myAdjustmentLog.NewValue);
            }
            if (myAdjustmentLog.OldValue == "" || myAdjustmentLog.OldValue == null)
            {
                myOldValue = 0;
            }
            else
            {
                myOldValue = Double.Parse(myAdjustmentLog.OldValue);
            }
            var changedQuantity = myNewValue - myOldValue;
            return changedQuantity;
        }
    }
}
