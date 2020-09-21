using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{

    enum Warehouse
    {
        New = 2,
        Available = 3,
        Consumable = 4
    }


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

    public class ObjectVersions
    {
        public string object_type { get; set; }
        public Dictionary<string, byte> object_version_dict { get; set; }
    }
    
    //public class ObjectVersions
    //{
    //    public Dictionary<string, byte> ObjectIDVersionXRef { get; set; }
    //}
}