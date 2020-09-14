﻿// System References
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
        RaygunClient myRaygunClient = new RaygunClient();

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantifyHelper QuantHelper = new QuantifyHelper();
            BoomiHelper BoomiHelper = new BoomiHelper();

            QuantHelper.QuantifyLogin();

            //***** Get all Transactions - will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            //MovementList all_Transactions = MovementList.GetMovementList((false, JobTreeNodeDisplayType.Name, Guid.Empty);
            MovementCollection all_Movements = MovementCollection.GetMovementCollection(MovementType.All);
            
            DataTable dt = new DataTable();
            dt.Columns.Add("Entity", typeof(string));
            dt.Columns.Add("QuantifyID", typeof(string));
            dt.Columns.Add("Version", typeof(string));

            Dictionary<string, Movement> myMovementsDictionary = new Dictionary<string, Movement>();

            foreach (Movement myMovement in all_Movements)
            {
                string myMovementNumber = myMovement.MovementNumber;
                DataRow myNewRow = dt.NewRow();
                myNewRow["Entity"] = "InventoryTrans";
                myNewRow["QuantifyID"] = myMovementNumber;
                string timestampVersion = "0x" + String.Join("", myMovement.VersionStamp.Select(b => Convert.ToString(b, 16)));
                myNewRow["Version"] = timestampVersion.ToString();

                dt.Rows.Add(myNewRow);

                //***** Build Dictionary *****
                if(!myMovementsDictionary.ContainsKey(myMovementNumber))
                {
                    myMovementsDictionary.Add(myMovementNumber, myMovement);
                } 
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                InventoryTransRootClass myInventoryTransactions = new InventoryTransRootClass();

                //***** Create audit log table structure *****
                DataTable auditLog = new DataTable();
                auditLog.Columns.Add("QuantifyID", typeof(string));
                auditLog.Columns.Add("Entity", typeof(string));
                auditLog.Columns.Add("PackageSchema", typeof(string));
                auditLog.Columns.Add("QuantifyDepartment", typeof(string));
                auditLog.Columns.Add("ProcessStatus", typeof(string));

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    string myMovementID = myRow["QuantifyID"].ToString();
                    Movement myMovement = myMovementsDictionary[myMovementID];
                    Order myOrder = Order.GetOrder(myMovement.OrderID, myMovement.BusinessPartnerID);
                    
                    MovementProductList myMovementProducts = MovementProductList.GetMovementProductList(myMovement.MovementID);
                    
                    //***** Populate Fields *****
                    InventoryTransData myInventoryTransData = new InventoryTransData();

                    myInventoryTransData.transaction_type = myMovement.TypeOfMovement.ToDescription();

                    //TODO: ADH 9/10/2020 - Do we need adjustment type? No field in Quantify like that, and transaction type will probably do the trick
                    //myInventoryTransData.adjustment_type = myMovement.type;

                    //TODO: ADH 9/10/2020 - How to handle line items of a transaction? Will need child list of part_numbers, etc.
                    //myInventoryTransData.part_number = myMovement.MovementProducts;

                    //***** Check Business Partner Type, set appropriate field *****
                    if (myMovement.BusinessPartnerType == PartnerTypes.Customer)
                    {
                        myInventoryTransData.customer_id = myMovement.BusinessPartnerNumber;
                    }
                    else
                    {
                        myInventoryTransData.vendor_id = myMovement.BusinessPartnerNumber;
                        myInventoryTransData.purchase_order_id = myOrder.PurchaseOrderNumber;
                    }

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    myInventoryTransactions.entity = "InventoryTrans";
                    myInventoryTransactions.InventoryTrans = myInventoryTransData;
                    string myJsonObject = JsonConvert.SerializeObject(myInventoryTransactions);

                    DataRow myNewRow = auditLog.NewRow();

                    myNewRow["QuantifyID"] = myInventoryTransData.inventory_trans_id;
                    myNewRow["Entity"] = "InventoryTrans";
                    myNewRow["PackageSchema"] = myJsonObject;
                    myNewRow["QuantifyDepartment"] = "";
                    myNewRow["ProcessStatus"] = "A";

                    auditLog.Rows.Add(myNewRow);
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
