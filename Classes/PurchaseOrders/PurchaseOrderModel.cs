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
    public class PurchaseOrderRootClass
    {
        public string entity { get; set; }
        public PurchaseOrderData PurchaseOrder { get; set; }
    }



    public class PurchaseOrderData
    {
        public PurchaseOrderData()
        {
            Lines = new List<PurchaseOrderLine>();
        }

        public string transaction_number { get; set; }
        public string order_number { get; set; }
        public string vendor_number { get; set; }
        public string to_warehouse { get; set; }
        public string notes { get; set; }
        public string entered_by { get; set; }
        public List<PurchaseOrderLine> Lines { get; set; }
    }

    public class PurchaseOrderLine
    {
        public string part_number { get; set; }
        public string quantity { get; set; }
        public string received_quantity { get; set; }
        public string cost { get; set; }
        public string unit_of_measure { get; set; }
    }
}