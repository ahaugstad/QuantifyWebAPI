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
    public class JobController : ApiController
    {
        // GET: api/Jobs
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Jobs/5
        public string Get(byte parmVersionStamp)
        {
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            //***** Get list of all jobsites *****
            StockingLocationList all_jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);

            //***** Loop through only jobs that have been updated since the last run date, sending each through as own call *****
            StockingLocationList jobsites = StockingLocationList.NewStockingLocationList();
            foreach (StockingLocationListItem jobsiteItem in all_jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                if(jobsite.VersionStamp[jobsite.VersionStamp.Length] != parmVersionStamp) 
                {
                    //***** Populate Fields *****
                    JobRootClass myDeserializedClass = new JobRootClass();
                    myDeserializedClass.JobData.job_id = jobsite.Number;
                    myDeserializedClass.JobData.job_name = jobsite.Description;
                    myDeserializedClass.JobData.site_name = jobsite.Name;
                    //***** Look up address to get individual address fields *****
                    // TO DO: how to do this? or is job address info accessible another way?
                    //Avontus.Rental.Library.Address jobAddress = Avontus.Rental.Library.Address.GetAddress(jobsite.ShippingAddress)
                    myDeserializedClass.JobData.site_address1 = jobsite.ShippingAddress;
                    //***** Look up customer to get customer ID *****
                    BusinessPartner jobCustomer = BusinessPartner.GetBusinessPartnerByName(jobsite.CustomerName);
                    myDeserializedClass.JobData.customer_id = jobCustomer.Name;
                    //***** Identify if job is sales taxable, and if so, assign a use tax code *****
                    if (jobsite.ConsumablesTaxable) 
                    { 
                        myDeserializedClass.JobData.sales_taxable = "Y";
                        // TO DO: will need to map this in Boomi?
                        myDeserializedClass.JobData.sales_tax_code = jobsite.JobTax1.Name; 
                    } 
                    else 
                    { 
                        myDeserializedClass.JobData.sales_taxable = "N";
                        myDeserializedClass.JobData.sales_tax_code = "";
                    }
                    myDeserializedClass.JobData.job_start_date = jobsite.StartDate;
                    myDeserializedClass.JobData.job_estimated_end_date = jobsite.StopDate;
                    myDeserializedClass.JobData.department = "20";
                    myDeserializedClass.JobData.job_type = "S";
                    // TO DO: where is the retention percentage?
                    // myDeserializedClass.JobData.retainage_percent = ?;
                    
                    // TO DO: serialize class to pass back to Boomi
                    //***** Serialize class into JObject to pass to Boomi ******
                    //JObject myJsonObject = myDeserializedClass;
                }
            }
            return "value";
        }
    }
}
