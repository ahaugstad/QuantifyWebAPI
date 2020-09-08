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
    public class JobBusinessLogic
    {
        RaygunClient myRaygunClient = new RaygunClient();

        // GET: api/Jobs/3
        public string Initialize()
        {
            //TODO: ADH 9/2/2020 - Implement Initial Load
            // Need to implement this - pass initial load of jobsite versionstamps to Boomi
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);



            //***** Loop through all jobsites and create list of jobsite ids and version stamps *****
            //StockingLocationList jobsites = StockingLocationList.NewStockingLocationList();
            //foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            //{
            //    StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);

            //}
            return "value";
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            //***** Get all jobsites - will loop through this and compare VersionStamp against appropriate record in our JobVersions dictionary *****
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);
            DataTable dt = new DataTable();
            dt.Columns.Add("Entity", typeof(string));
            dt.Columns.Add("QuantifyID", typeof(string));
            dt.Columns.Add("Version", typeof(string));

            Dictionary<string, StockingLocation> myJobsDictionary = new Dictionary<string, StockingLocation>();

            foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                string myJobsiteNumber = jobsite.Number;
                DataRow myNewRow = dt.NewRow();
                myNewRow["Entity"] = "Job";
                myNewRow["QuantifyID"] = myJobsiteNumber;
                myNewRow["Version"] = jobsite.VersionStamp[jobsite.VersionStamp.Length - 1].ToString();

                dt.Rows.Add(myNewRow);

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

                //***** Create audit log table structure *****
                DataTable auditLog = new DataTable();
                auditLog.Columns.Add("QuantifyID", typeof(string));
                auditLog.Columns.Add("Entity", typeof(string));
                auditLog.Columns.Add("PackageSchema", typeof(string));
                auditLog.Columns.Add("QuantifyDepartment", typeof(string));
                auditLog.Columns.Add("ProcessStatus", typeof(string));

                foreach (DataRow myRow in myChangedRecords.Rows)
                {
                    string myJobID = myRow["QuantifyID"].ToString();
                    StockingLocation jobsite = myJobsDictionary[myJobID];
                    //***** Populate Fields *****
                    JobData myJobData = new JobData();

                    myJobData.job_id = jobsite.Number;
                    myJobData.job_name = jobsite.Description;
                    myJobData.site_name = jobsite.Name;
                    //***** Look up address to get individual address fields *****

                    //TODO: ADH 9/1/2020 - Dev/Avontus Question 
                    // How to do this? or is job address info accessible another way?
                    //Avontus.Rental.Library.Address jobAddress = Avontus.Rental.Library.Address.GetAddress(jobsite.ShippingAddress)
                    myJobData.site_address1 = jobsite.ShippingAddress;

                    //***** Look up customer to get customer ID *****
                    BusinessPartner jobCustomer = BusinessPartner.GetBusinessPartnerByName(jobsite.CustomerName);
                    myJobData.customer_id = jobCustomer.Name;
                    //***** Identify if job is sales taxable, and if so, assign a use tax code *****
                    if (jobsite.ConsumablesTaxable)
                    {
                        myJobData.sales_taxable = "Y";
                        // TO DO: will need to map this in Boomi?
                        //myJobData.sales_tax_code = jobsite.JobTax1.Name;
                        myJobData.sales_tax_code = "3000";
                    }
                    else
                    {
                        myJobData.sales_taxable = "N";
                        myJobData.sales_tax_code = "";
                    }
                    myJobData.job_start_date = jobsite.StartDate;
                    myJobData.job_estimated_end_date = jobsite.StopDate;

                    //TODO: ADH 9/3/2020 - Identify where branch office is and how it relates to job in data
                    myJobData.department = "3005";

                    //TODO: ADH 9/3/2020 - Confirm this is accurate for grabbing Job Type from List
                    //(mapping to WebApps codes will be done in Boomi, just need field)
                    //JobList1 myJobTypeList = JobList1.GetJobList1(jobsite.JobList1ID);
                    //myJobData.job_type = myJobTypeList.Name;
                    myJobData.job_type = "S";

                    //TODO: ADH 9/3/2020 - Uncomment next two lines when retention configured in Quantify
                    //JobList2 myJobTypeList = JobList2.GetJobList2(jobsite.JobList2ID);
                    //myJobData.retainage_percent = myJobTypeList.Name;
                    myJobData.retainage_percent = "15";

                    //***** Package as class, serialize to JSON and write to audit log table *****
                    myJobs.entity = "Job";
                    myJobs.Job = myJobData;
                    string myJsonObject = JsonConvert.SerializeObject(myJobs);

                    DataRow myNewRow = auditLog.NewRow();

                    myNewRow["QuantifyID"] = myJobData.job_id;
                    myNewRow["Entity"] = "Job";
                    myNewRow["PackageSchema"] = myJsonObject;
                    myNewRow["QuantifyDepartment"] = "";
                    myNewRow["ProcessStatus"] = "A";

                    auditLog.Rows.Add(myNewRow);
                }
                //***** Create audit log record for Boomi to go pick up *****
                // REST API URL: http://apimariaasad01.apigroupinc.api:9090/ws/rest/webapps_quantify/api
                DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

                //TODO: ADH 9 / 4 / 2020 - Figure out why following line is failing
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
                PostBoomiAPI();
            }
            return success;
        }
        public async Task PostBoomiAPI()
        {
            //***** REST API URL for Boomi web service *****
            string uriString = "http://apimariaasad01.apigroupinc.api:9090/ws/rest/webapps_quantify/api";

            //***** Create ping JSON string so that Boomi has an object to get passed - does not matter what it contains *****
            PingRootClass myPingClass = new PingRootClass();
            myPingClass.pingString = "Hi Boomi";
            string myPingJson = JsonConvert.SerializeObject(myPingClass);

            //***** Using HttpClient approach *****
            HttpClient client = new HttpClient();
            using (client)
            {
                client.BaseAddress = new Uri(uriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                //GET Method
                HttpContent httpContent = new StringContent(myPingJson);
                HttpResponseMessage response = await client.PostAsync(uriString, httpContent);
                return;
            }
        }
    }
}