// System References
using System;
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

namespace QuantifyWebAPI.Controllers
{
    public class JobBusinessLogic
    {
        // GET: api/Jobs
        public IEnumerable<string> GetTest()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Jobs/3
        public string Initialize()
        {
            //TODO: ADH 9/2/2020 - Implement Initial Load
            // Need to implement this - pass initial load of jobsite versionstamps to Boomi
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);
            


            //***** Loop through all jobsites and create list of jobsite ids and version stamps *****
            StockingLocationList jobsites = StockingLocationList.NewStockingLocationList();
            foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                
            }
            return "value";
        }

        public string Post(JObject jsonResult)
        {
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            //***** Get all jobsites - will loop through this and compare VersionStamp against appropriate record in our JobVersions dictionary *****
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);

            //***** Initialize deserialized classes to be used *****
            ObjectGetRequest myObjectVersions = jsonResult.ToObject<ObjectGetRequest>();
            JobRootClass myJobs = new JobRootClass();

            //***** Loop through only jobs that have been updated since the last run date, sending each through as own call *****
            StockingLocationList jobsites = StockingLocationList.NewStockingLocationList();
            foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                //string JobID = 
                //if(jobsite.VersionStamp[jobsite.VersionStamp.Length]) 
                //{
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
                        myJobData.sales_tax_code = jobsite.JobTax1.Name; 
                    } 
                    else
                    {
                        myJobData.sales_taxable = "N";
                        myJobData.sales_tax_code = "";
                    }
                    myJobData.job_start_date = jobsite.StartDate;
                    myJobData.job_estimated_end_date = jobsite.StopDate;
                    myJobData.department = "20";
                    myJobData.job_type = "S";

                    //TODO: ADH 9/1/2020 - Dev/Avontus Question
                    // Where is the retention percentage?
                    // myDeserializedClass.JobData.retainage_percent = ?;

                    myJobs.JobList.Add(myJobData);
                //}
            }
            string myJsonObject = JsonConvert.SerializeObject(myJobs);
            return myJsonObject;
        }
    }
}
