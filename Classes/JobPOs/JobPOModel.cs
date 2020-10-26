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
    public class JobPORootClass
    {
        public string entity { get; set; }
        public JobPOData JobPO { get; set; }
    }



    public class JobPOData
    {
        public string transaction_number { get; set; }
        public string job_number { get; set; }
        public string vendor_number { get; set; }
        public string total { get; set; }
        public string entered_by { get; set; }
    }
}