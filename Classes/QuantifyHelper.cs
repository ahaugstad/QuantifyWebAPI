using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

// Other References
using Newtonsoft.Json;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;



namespace QuantifyWebAPI.Classes
{
    public class QuantifyHelper
    {
        public void QuantifyLogin()
        /*
         * Connection settings are controlled by the regular Quantify Client. Please
         * start Quantify and verify you can log in. Once you can log in with
         * Quantify, the API will use the same connection strings
         */
        {
            AvontusPrincipal.Logout();
            Boolean success = AvontusPrincipal.Login("Alex.Haugstad", "Scaffold");
            if (success)
            {
                Console.WriteLine("Login successful");
            }
            else
            {
                Console.WriteLine("Login failed");
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }
        }
    }
}