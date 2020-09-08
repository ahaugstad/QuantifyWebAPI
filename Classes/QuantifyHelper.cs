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
    public class QuantifyHelper
    {
        RaygunClient myRaygunClient = new RaygunClient();
        public void QuantifyLogin()
        /*
         * Connection settings are controlled by the regular Quantify Client. Please
         * start Quantify and verify you can log in. Once you can log in with
         * Quantify, the API will use the same connection strings
         */
        {
            try
            {
                AvontusPrincipal.Logout();
                Boolean success = AvontusPrincipal.Login("Alex.Haugstad", "Scaffold");
                if (success)
                {
                    Console.WriteLine("Login successful");
                }
                else
                {
                    throw new System.ArgumentException("Login failed");
                }
            }
            catch (Exception ex)
            {
                myRaygunClient.SendInBackground(ex);
            }
        }
    }
}