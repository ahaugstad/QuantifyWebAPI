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
    public class InventoryTransRootClass
    {
        public string entity { get; set; }
        public InventoryTransData InventoryTrans { get; set; }
    }

    public class InventoryTransData
    {
        public string inventory_trans_id { get; set; }
        public string transaction_type { get; set; }
        public string package_type { get; set; }
        public string adjustment_type { get; set; }
        public string custvend_id { get; set; }
        public string order_id { get; set; }
        public List<InventoryTransLines> Lines { get; set; }
    }

    public class InventoryTransLines
    {
        public string part_number { get; set; }
        public string serial_number { get; set; }
        public int quantity { get; set; }
        public string catalog { get; set; }
        public string comment { get; set; }
    }
    
}