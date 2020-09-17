using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class ProductRootClass
    {
        public string entity { get; set; }
        public ProductData Product { get; set; }
    }

    public class ProductData
    {
        public string product_id { get; set; }
        public string description { get; set; }
        public string category { get; set; }
        public string list_price { get; set; }
        public string unit_cost { get; set; }
        public string catalog { get; set; }
    }
}