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
using Avontus.Rental.Utility;
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
        string initializationMode;

        public InvoiceBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all invoices - will loop through this and compare VersionStamp against appropriate record in Versions table *****
            InvoiceList all_Invoices_List = InvoiceList.GetInvoiceList(InvoiceExportStatus.All);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, InvoiceListItem> myInvoicesDictionary = new Dictionary<string, InvoiceListItem>();

            foreach (InvoiceListItem myInvoiceListItem in all_Invoices_List)
            {
                string myInvoiceNumber = myInvoiceListItem.InvoiceNumber;
                DateTime myModifyDate = Convert.ToDateTime(myInvoiceListItem.LastModifyDate);
                string timestampVersion = myModifyDate.Ticks.ToString();

                //***** Add record to data table to be written to Version table in SQL *****
                dt = MySqlHelper.CreateVersionDataRow(dt, "Invoice", myInvoiceNumber, timestampVersion.ToString());

                //***** Build Dictionary *****
                if (!myInvoicesDictionary.ContainsKey(myInvoiceNumber))
                {
                    myInvoicesDictionary.Add(myInvoiceNumber, myInvoiceListItem);
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
                    InvoiceRootClass myInvoices = new InvoiceRootClass();

                    //***** Create Audit Log and XRef table structures *****            
                    DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        InvoiceData myInvoiceData = new InvoiceData();

                        //***** Initalize fields and classes to be used to build data profile *****
                        string myInvoiceID = myRow["QuantifyID"].ToString();
                        InvoiceListItem myInvoiceListItem = myInvoicesDictionary[myInvoiceID];
                        Invoice myInvoice = Invoice.GetInvoice(myInvoiceListItem.InvoiceID, true);

                        //***** Build header data profile *****
                        myInvoiceData.invoice_id = myInvoiceID;
                        myInvoiceData.job_number = myInvoice.JobSite.Number;
                        myInvoiceData.invoice_date = myInvoice.InvoiceDateTime.ToShortDateString();
                        myInvoiceData.invoice_total = myInvoice.TotalInvoice.ToString();

                        //***** Evaluate Job Sales Tax Code - all jobs should have these before integration starts, so throw error if they don't *****
                        if (myInvoice.JobTax1ID != null && myInvoice.JobTax1ID != Guid.Empty)
                        {
                            myInvoiceData.sales_tax_code = myInvoice.JobTax1.ToString();
                        }
                        else
                        {
                            myErrorText = "Jobsite sales tax code is blank. Please provide a sales tax code on this invoice's associated jobsite to integrate this invoice.";
                            myProcessStatus = "E1";
                        }

                        //***** Build data profile for Rent subtotal line item *****
                        InvoiceTransLine myInvoiceRentLine = new InvoiceTransLine();
                        myInvoiceRentLine.amount = myInvoice.TotalRent.ToString();
                        myInvoiceRentLine.description = "Rent";
                        myInvoiceRentLine.taxable = myInvoice.RentIsTaxableText;
                        //TODO: ADH 10/6/2020 - BUSINESS QUESTION: Identify which cost code corresponds to rental lines
                        myInvoiceRentLine.cost_code = "10";
                        myInvoiceData.Lines.Add(myInvoiceRentLine);

                        //***** Build data profile for Product subtotal line item *****
                        InvoiceTransLine myInvoiceProductLine = new InvoiceTransLine();
                        myInvoiceRentLine.amount = myInvoice.TotalProductCharge.ToString();
                        myInvoiceRentLine.description = "Product Charge";
                        myInvoiceRentLine.taxable = myInvoice.ConsumablesAreTaxableText;
                        //TODO: ADH 10/6/2020 - BUSINESS QUESTION: Identify which cost code corresponds to product charge lines
                        myInvoiceRentLine.cost_code = "20";
                        myInvoiceData.Lines.Add(myInvoiceProductLine);

                        //***** Build non-rental line item data profile *****
                        foreach (InvoiceUnitPrice myInvoiceUnitPrice in myInvoice.InvoiceUnitPrices)
                        {
                            //***** Data structure puts charge types that have values on invoice at the top, and then loops through all other charges, regardless if they have values *****
                            //***** This if statement breaks out of the loop once we are done evaluating charges that actually have amounts *****
                            if (myInvoiceUnitPrice.UnitPriceTotal.ToString() != "")
                            {
                                InvoiceTransLine myInvoiceTransLine = new InvoiceTransLine();
                                myInvoiceTransLine.amount = myInvoiceUnitPrice.UnitPriceTotal.ToString();
                                myInvoiceTransLine.description = myInvoiceUnitPrice.InvoiceDescription;
                                myInvoiceTransLine.taxable = myInvoiceUnitPrice.TaxableText;

                                //TODO: ADH 10/1/2020 - Verify which of these lines we should use (neither populate for every charge type)
                                //***** Evaluate line cost code - all lines need cost code associated with them, so throw error if they don't *****
                                //myInvoiceTransLine.cost_code = myInvoiceUnitPrice.CostCode;
                                if (myInvoiceUnitPrice.RevenueCode != null && myInvoiceUnitPrice.RevenueCode != "")
                                {
                                    myInvoiceTransLine.cost_code = myInvoiceUnitPrice.RevenueCode;
                                }
                                else
                                {
                                    myErrorText = "Cost code for non-rental item line is blank. Please ensure every non-rental line has a cost code to integrate this invoice.";
                                    myProcessStatus = "E1";
                                }
                                myInvoiceData.Lines.Add(myInvoiceTransLine);
                            }
                            else
                            {
                                break;
                            }
                        }

                        //***** Package as class, serialize to JSON and write to audit log table *****
                        myInvoices.entity = "Invoice";
                        myInvoices.Invoice = myInvoiceData;
                        string myJsonObject = JsonConvert.SerializeObject(myInvoices);

                        //***** Create audit log datarow ******                 
                        auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Invoice", myInvoiceData.invoice_id, myJsonObject, "", myProcessStatus, myErrorText);
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
