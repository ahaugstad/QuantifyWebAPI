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
        QuantifyCredentials myQuantifyCredentials;
        public QuantifyHelper(QuantifyCredentials QuantifyConnCreds)
        {
            this.myRaygunClient = myRaygunClient;
            myQuantifyCredentials = QuantifyConnCreds;
        }

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
                //Boolean success = AvontusPrincipal.Login("Alex.Haugstad", "Scaffold");
                Boolean success = AvontusPrincipal.Login(myQuantifyCredentials.UserName, myQuantifyCredentials.Password);

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

    public class QuantifyCredentials
    {
        public QuantifyCredentials(string userName, string password)
        {
            UserName = userName;
            Password = password;
        }

        public string UserName { get; set; }
        public string Password { get; set; }
    }
}