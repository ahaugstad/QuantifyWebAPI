using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class TaxCodeRootClass
    {

        public string entity { get; set; }
        public TaxCodeData TaxCodeData { get; set; }
    }

    public class TaxCodeData
    {
        public string tax_code { get; set; }
        public string tax_code_description { get; set; }
        public string tax_code_state { get; set; }
        public string tax_code_rate { get; set; }
    }

    public class TaxCodeResponseObj
    {
        public string status { get; set; }
        public List<string> errorList { get; set; }
        public TaxCodeResponseObj()
        {
            errorList = new List<string>();
        }
    }
}