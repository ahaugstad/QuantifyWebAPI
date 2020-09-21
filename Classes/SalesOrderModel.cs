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
    public class SalesOrderRootClass
    {
        public string entity { get; set; }
        public SalesOrderData SalesOrder { get; set; }
    }



    public class SalesOrderData
    {
        public SalesOrderData()
        {
            Lines = new List<SalesOrderLine>();
        }

        public string transaction_number { get; set; }
        public string job_number { get; set; }
        public string customer_number { get; set; }
        public string reference_number { get; set; }
        public string branch_office { get; set; }
        public string from_warehouse { get; set; }
        public string notes { get; set; }
        public string transaction_date { get; set; }
        public List<SalesOrderLine> Lines { get; set; }
    }

    public class SalesOrderLine
    {
        public string part_number { get; set; }
        public string quantity { get; set; }
        public string price_ea { get; set; }
        public string unit_of_measure { get; set; }
    }
}