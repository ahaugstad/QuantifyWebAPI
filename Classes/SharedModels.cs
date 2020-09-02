using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace QuantifyWebAPI.Classes
{
    public class Address
    {
        public string addressTypeCode { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string zip { get; set; }
        public string country { get; set; }
    }

    public class Contact
    {
        public string contact_name { get; set; }
        public string contact_phone { get; set; }
        public string contact_email { get; set; }
    }

    public class ObjectGetRequest
    {
        public string object_type { get; set; }
        public List<ObjectVersions> object_version_list { get; set; }
    }
    
    public class ObjectVersions
    {
        public string object_id { get; set; }
        public byte object_version { get; set; }
    }
}