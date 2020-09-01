using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class JobRootClass
    {
        public string entity { get; set; }
        public JobData JobData { get; set; }
    }

    public class JobData
    {
        public string job_id { get; set; }
        public string job_name { get; set; }
        public string site_name { get; set; }
        public string site_address1 { get; set; }
        public string site_city { get; set; }
        public string site_state { get; set; }
        public string site_zip { get; set; }
        public string customer_id { get; set; }
        public string sales_taxable { get; set; }
        public string sales_tax_code { get; set; }
        public string job_start_date { get; set; }
        public string job_estimated_end_date { get; set; }
        public string department { get; set; }
        public string job_type { get; set; }
        public string retainage_percent { get; set; }
    }

    public class JobResponseObj
    {
        public string status { get; set; }
        public List<string> errorList { get; set; }
        public JobResponseObj()
        {
            errorList = new List<string>();
        }
    }
}