using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Address
    {
        public string addressTypeCode { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string zip { get; set; }
    }

    public class Contact
    {
        public string contact_name { get; set; }
        public string contact_phone { get; set; }
        public string contact_email { get; set; }
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

    public class CustomerRootClass
    {
        public string entity { get; set; }
        public CustomerData CustomerData { get; set; }
    }
}