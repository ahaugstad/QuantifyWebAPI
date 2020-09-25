// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class VendorRootClass
    {
        public string entity { get; set; }
        public VendorData VendorData { get; set; }
    }

    public class VendorData
    {
        public VendorData()
        {
            Addresses = new List<Address>();
        }

        public string vendor_id { get; set; }
        public string vendor_name { get; set; }
        public string vendor_phone { get; set; }
        public string vendor_email { get; set; }
        public string vendor_fax { get; set; }
        public string is_active { get; set; }
        public List<Address> Addresses { get; set; }
    }

    public class VendorResponseObj
    {
        public string status { get; set; }
        public List<string> errorList { get; set; }
        public VendorResponseObj()
        {
            errorList = new List<string>();
        }
    }


}