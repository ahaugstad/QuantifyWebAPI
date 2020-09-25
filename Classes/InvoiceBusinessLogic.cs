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
    public class InvoiceBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper;

        public InvoiceBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all invoices - will loop through this and compare VersionStamp against appropriate record in Versions table *****
            InvoiceCollection all_Invoices = InvoiceCollection.GetInvoiceCollection(InvoiceExportStatus.All);
            InvoiceList all_Invoices_List = InvoiceList.GetInvoiceList(InvoiceExportStatus.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Invoice> myInvoicesDictionary = new Dictionary<string, Invoice>();

            foreach (Invoice myInvoice in all_Invoices)
            {
                //TODO: ADH 9/24/2020 - Apply criteria for which Invoices we will be considering in process, if any
                //if (
                //    //myInvoice.
                //    )
                //{
                    string myInvoiceNumber = myInvoice.InvoiceNumber;
                    //TODO: ADH 9/24/2020 - Modify line below to send numerical representation of date (cast as string) as timestamp
                    string timestampVersion = myInvoice.ModifyDate.ToString();
                    //string timestampVersion = "0x" + String.Join("", myInvoice.ModifyDate.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "Invoice", myInvoiceNumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myInvoicesDictionary.ContainsKey(myInvoiceNumber))
                    {
                        myInvoicesDictionary.Add(myInvoiceNumber, myInvoice);
                    }
                //}
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                InvoiceRootClass myInvoices = new InvoiceRootClass();

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    //***** Initialize error tracking fields and data package *****
                    string myErrorText = "";
                    string myProcessStatus = "A";
                    InvoiceData myInvoiceData = new InvoiceData();

                    //***** Initalize fields and classes to be used to build data profile *****
                    string myInvoiceID = myRow["QuantifyID"].ToString();
                    Invoice myInvoice = myInvoicesDictionary[myInvoiceID];
                    InvoiceProductChargeCollection myInvoiceProductCharges = InvoiceProductChargeCollection.GetInvoiceProductChargeCollection(myInvoice.InvoiceID);

                    //***** Build header data profile *****
                    myInvoiceData.invoice_id = myInvoiceID;
                    myInvoiceData.customer_number = myInvoice.Customer.PartnerNumber;
                    myInvoiceData.job_number = myInvoice.JobSite.Number;
                    myInvoiceData.branch_office = myInvoice.JobSite.ParentBranchOrLaydown.Number;
                    myInvoiceData.invoice_date = myInvoice.InvoiceDateTime.ToShortDateString();
                    myInvoiceData.invoice_total = myInvoice.TotalInvoice.ToString();

                    //***** Assign warehouse based on type of movement *****
                    //switch (myInvoice.TypeOfMovement)
                    //{
                    //    case MovementType.SellNew:
                    //        myInvoiceData.from_warehouse = ((int)Warehouse.New).ToString();
                    //        break;
                    //    case MovementType.SellForRent:
                    //        myInvoiceData.from_warehouse = ((int)Warehouse.Available).ToString();
                    //        break;
                    //    case MovementType.SellConsumables:
                    //        myInvoiceData.from_warehouse = ((int)Warehouse.Consumable).ToString(); 
                    //        break;
                    //}
                    
                    //***** Build line item data profile *****
                    foreach (InvoiceProductCharge invoiceProductCharge in myInvoiceProductCharges)
                    {
                        //Product myProduct = Product.GetProduct(InvoiceProductListItem.BaseProductID);
                        //InvoiceLine myInvoiceLine = new InvoiceLine();
                        //myInvoiceLine.part_number = InvoiceProductListItem.PartNumber;
                        //myInvoiceLine.quantity = InvoiceProductListItem.Quantity.ToString();
                        //myInvoiceLine.price_ea = InvoiceProductListItem.SellPrice.ToString();
                        //myInvoiceLine.unit_of_measure = myProduct.UnitOfMeasureName;
                        //myInvoiceData.Lines.Add(myInvoiceLine);
                    }

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    myInvoices.entity = "Invoice";
                    myInvoices.Invoice = myInvoiceData;
                    string myJsonObject = JsonConvert.SerializeObject(myInvoices);

                    //***** Create audit log datarow ******                 
                    //auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Invoice", myInvoiceData.transaction_number, myJsonObject, "", myProcessStatus, myErrorText);
                    auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Invoice", myInvoiceData.transaction_number, myJsonObject, "", myProcessStatus);
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
