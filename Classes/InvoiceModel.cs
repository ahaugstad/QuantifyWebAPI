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
    public class InvoiceRootClass
    {
        public string entity { get; set; }
        public InvoiceData Invoice { get; set; }
    }



    public class InvoiceData
    {
        public InvoiceData()
        {
            Lines = new List<InvoiceTransLine>();
        }

        public string invoice_id { get; set; }
        public string job_number { get; set; }
        public string invoice_date { get; set; }
        public string sales_tax_code { get; set; }
        public string invoice_total { get; set; }
        public List<InvoiceTransLine> Lines { get; set; }
    }

    public class InvoiceTransLine
    {
        public string amount { get; set; }
        public string cost_code { get; set; }
        public string description { get; set; }
        public string taxable { get; set; }
    }
}