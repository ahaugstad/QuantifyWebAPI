using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

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
        public string customer_id { get; set; }
        public string customer_name { get; set; }
        public string customer_phone { get; set; }
        public string customer_email { get; set; }
        public string customer_fax { get; set; }
        public List<Address> Addresses { get; set; }
        public List<Contact> Contacts { get; set; }
    }
}