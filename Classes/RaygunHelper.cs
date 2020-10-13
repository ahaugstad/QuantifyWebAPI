using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

// Other References
using Newtonsoft.Json;
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
    public class RaygunExceptionPackage
    {
        public IList<string> Tags = new List<string>();
        public System.Collections.IDictionary CustomData = new Dictionary<string, string>();
        public Exception Ex = new Exception();
    }
}