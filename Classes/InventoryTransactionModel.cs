using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class InventoryTransactionRootClass
    {
        public string entity { get; set; }
        public InventoryTransData Job { get; set; }
    }

    public class InventoryTransData
    {
        public string adjustmentType { get; set; }
        public string warehouse { get; set; }
        public string part_number { get; set; }
        public int quantity { get; set; }
        public string coment { get; set; }
      
    }
}