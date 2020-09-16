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

        public string myPurchaseOrderData { get; set; }
        public string transaction_type { get; set; }
        public string ReferanceNumber { get; set; }
        public string BranchOffice { get; set; }
        public string Vendor { get; set; }
        public string Notes { get; set; }
        public string Order { get; set; }
        public DateTime Date { get; set; }
        public List<PurchaseOrderLine> Lines { get; set; }
    }

    public class PurchaseOrderLine
    {
        public string part_number { get; set; }
        public int quantity { get; set; }
        public double Cost { get; set; }
        public string UnitOfMeasure { get; set; }
    }
    
}