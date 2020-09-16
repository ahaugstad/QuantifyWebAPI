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
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper = new QuantifyHelper();
        BoomiHelper BoomiHelper = new BoomiHelper();

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all Transactions - will loop through this and compare VersionStamp against appropriate record in our TransactionVersions dictionary *****
            //MovementList all_Transactions = MovementList.GetMovementList((false, JobTreeNodeDisplayType.Name, Guid.Empty);
            MovementCollection all_Movements = MovementCollection.GetMovementCollection(MovementType.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myMovementsDictionary = new Dictionary<string, Movement>();

            foreach (Movement myMovement in all_Movements)
            {
                string myMovementNumber = myMovement.MovementNumber;
                string timestampVersion = "0x" + String.Join("", myMovement.VersionStamp.Select(b => Convert.ToString(b, 16)));

                //***** Add record to data table to be written to Version table in SQL *****
                dt = MySqlHelper.CreateVersionDataRow(dt, "InventoryTrans", myMovementNumber, timestampVersion.ToString());

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

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    //***** Initalize fields and classes to be used to build data profile *****
                    string myMovementID = myRow["QuantifyID"].ToString();
                    Movement myMovement = myMovementsDictionary[myMovementID];
                    Order myOrder = Order.GetOrder(myMovement.OrderID);
                    MovementProductList myMovementProducts = MovementProductList.GetMovementProductList(myMovement.MovementID);
                    
                    //***** Build header data profile *****
                    InventoryTransData myInventoryTransData = new InventoryTransData();
                    myInventoryTransData.Lines = new List<InventoryTransLine>();
                    myInventoryTransData.inventory_trans_id = myMovementID;
                    myInventoryTransData.transaction_type = myMovement.TypeOfMovement.ToDescription();
                    myInventoryTransData.package_type = myMovement.BusinessPartnerType.ToDescription();
                    //TODO: ADH 9/10/2020 - Do we need adjustment type? No field in Quantify like that, and transaction type will probably do the trick
                    //myInventoryTransData.adjustment_type = myMovement.type;
                    myInventoryTransData.custvend_id = myMovement.BusinessPartnerNumber;

                    //***** Check Business Partner Type, set appropriate field *****
                    if (myMovement.BusinessPartnerType == PartnerTypes.Customer)
                    {
                        //TODO: ADH 9/14/2020 - BUSINESS QUESTION: Can you associate an invoice to a movement? Is that how it works?
                        myInventoryTransData.package_type = "Customer";
                        myInventoryTransData.order_id = myOrder.PurchaseOrderNumber;
                    }
                    else
                    {
                        myInventoryTransData.package_type = "Vendor";
                        myInventoryTransData.order_id = myMovement.BackOrderNumber;
                    }

                    //***** Build line item data profile *****
                    foreach (MovementProductListItem movementProductListItem in myMovementProducts)
                    {
                        Product myProduct = Product.GetProduct(movementProductListItem.BaseProductID);
                        InventoryTransLine myTransLine = new InventoryTransLine();
                        myTransLine.part_number = movementProductListItem.PartNumber;
                        myTransLine.serial_number = myProduct.SerialNumber;
                        myTransLine.quantity = movementProductListItem.Quantity.ToString();
                        myTransLine.catalog = myProduct.ProductType.ToDescription();
                        myTransLine.comment = movementProductListItem.Comment;
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