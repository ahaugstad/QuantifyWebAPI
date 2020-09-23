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
    public class JobBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper;

        public JobBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all jobsites - will loop through this and compare VersionStamp against appropriate record in our JobVersions dictionary *****
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);


            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, StockingLocation> myJobsDictionary = new Dictionary<string, StockingLocation>();

            foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                string myJobsiteNumber = jobsite.Number;
                string timestampVersion = "0x" + String.Join("", jobsite.VersionStamp.Select(b => Convert.ToString(b, 16)));

                //***** Add record to data table to be written to Version table in SQL *****
                dt = MySqlHelper.CreateVersionDataRow(dt, "Job", myJobsiteNumber, timestampVersion.ToString());           

                //***** Build Dictionary *****
                if(!myJobsDictionary.ContainsKey(myJobsiteNumber))
                {
                    myJobsDictionary.Add(myJobsiteNumber, jobsite);
                } 
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            if (myChangedRecords.Rows.Count > 0)
            {
                JobRootClass myJobs = new JobRootClass();

                //***** Create Audit Log and XRef table structures *****            
                DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    string myJobID = myRow["QuantifyID"].ToString();
                    StockingLocation jobsite = myJobsDictionary[myJobID];
                    
                        //***** Populate Fields *****
                        JobData myJobData = new JobData();

                        myJobData.job_id = jobsite.Number;
                        myJobData.job_name = jobsite.Description;
                        myJobData.site_name = jobsite.Name;
                        //***** Look up shipping address to get individual address fields *****
                        Avontus.Rental.Library.Address jobShippingAddress = jobsite.Addresses.GetAddressByType(AddressTypes.Shipping);
                        myJobData.site_address1 = jobShippingAddress.Street;
                        myJobData.site_city = jobShippingAddress.City;
                        myJobData.site_state = jobShippingAddress.StateName;
                        myJobData.site_zip = jobShippingAddress.PostalCode;

                        //***** Look up customer to get customer ID *****
                        BusinessPartner jobCustomer = BusinessPartner.GetBusinessPartner(jobsite.BusinessPartnerID);
                        myJobData.customer_id = jobCustomer.AccountingID;

                        //***** Identify if job is sales taxable, and if so, assign a use tax code (mapping to WebApps accordingly will be done in Boomi) *****
                        if (jobsite.JobTax1ID != null && jobsite.JobTax1ID != Guid.Empty)
                        {
                            myJobData.sales_taxable = "Y";
                            myJobData.sales_tax_code = jobsite.JobTax1.Name;
                        }
                        else
                        {
                            myJobData.sales_taxable = "N";
                            myJobData.sales_tax_code = "";
                        }
                        myJobData.job_start_date = jobsite.StartDate;
                        //TODO: ADH 9/8/2020 - Identify if we need a different field for this; I think this is the actual job stop date
                        myJobData.job_estimated_end_date = jobsite.StopDate;

                        //***** Branch office - may need to drill up/down more, depending *****
                        myJobData.department = jobsite.ParentBranchOrLaydown.Number;

                        //***** Job Type (will be mapped to WebApps Job Types in Boomi) *****
                        if (jobsite.JobList1ID != null && jobsite.JobList1ID != Guid.Empty)
                        {
                            JobList1 myJobTypeList1 = JobList1.GetJobList1(jobsite.JobList1ID);
                            myJobData.job_type = myJobTypeList1.Name;
                        }
                        else
                        {
                            myJobData.job_type = "Non-Scaffold";
                        }

                        //***** Retainage Amount (will be mapped appropriately to WebApps Retention field in Boomi) *****
                        if (jobsite.JobList2ID != null && jobsite.JobList2ID != Guid.Empty)
                        {
                            JobList2 myJobTypeList2 = JobList2.GetJobList2(jobsite.JobList2ID);
                            myJobData.retainage_percent = myJobTypeList2.Name;
                        }
                        else
                        {
                            myJobData.retainage_percent = "No Retainage";
                        }

                    //***** Skip all jobs of type Non-Scaffold - will not be integrating these over *****
                    if (myJobData.job_type != "Non-Scaffold")
                    {
                        //***** Package as class, serialize to JSON and write to audit log table *****
                        myJobs.entity = "Job";
                        myJobs.Job = myJobData;
                        string myJsonObject = JsonConvert.SerializeObject(myJobs);

                        //***** Create audit log datarow ******                 
                        auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Job", myJobData.job_id, myJsonObject, "", "A");
                  
                    }
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
            return success;
        }
    }
}
