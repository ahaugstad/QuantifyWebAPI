using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class CustomerRootClass
    {

        public string entity { get; set; }
        public CustomerData CustomerData { get; set; }
    }

    public class CustomerData
    {
        public CustomerData()
        {
            Addresses = new List<Address>();
            Contacts = new List<Contact>();
        }

        public string customer_id { get; set; }
        public string customer_name { get; set; }
        public string customer_phone { get; set; }
        public string customer_email { get; set; }
        public string customer_fax { get; set; }
        public string is_active { get; set; }
        public List<Address> Addresses { get; set; }
        public List<Contact> Contacts { get; set; }
    }

    public class CustomerResponseObj
    {
        public string status { get; set; }
        public List<string> errorList { get; set; }
        public CustomerResponseObj()
        {
            errorList = new List<string>();
        }
    }
}