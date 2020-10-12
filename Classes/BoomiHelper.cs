// System References
using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Mail;
using System.Web.Http;
using System.Data.SqlClient;
using System.Text;
using System.Web.Management;
using System.Drawing;
using System.Threading.Tasks;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;

// Internal Class references
using QuantifyWebAPI.Classes;

// Other References
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;



namespace QuantifyWebAPI.Classes
{
    public class BoomiHelper
    {
        RaygunClient myRaygunClient = new RaygunClient();
        public async Task PostBoomiAPI()
        {
            try
            {
                //***** REST API URL for Boomi web service *****
                string uriString = "http://apimariadbap01.apigroupinc.api:9090/ws/rest/webapps_quantify/api";
                //TODO: ADH 10/12/2020 - When going to prod, use following URL instead to hit load balancer
                //string uriString = "http://apiboomiprod.apigroupinc.com:9090/ws/rest/webapps_quantify/api";

                //***** Create ping JSON string so that Boomi has an object to get passed - does not matter what it contains *****
                PingRootClass myPingClass = new PingRootClass();
                myPingClass.pingString = "Hi Boomi";
                string myPingJson = JsonConvert.SerializeObject(myPingClass);

                //***** Using HttpClient approach *****
                HttpClient client = new HttpClient();
                using (client)
                {
                    client.BaseAddress = new Uri(uriString);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    //GET Method
                    HttpContent httpContent = new StringContent(myPingJson);
                    HttpResponseMessage response = await client.PostAsync(uriString, httpContent);
                    return;
                }
            }
            catch (Exception ex)
            {
                myRaygunClient.SendInBackground(ex);
            }
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
                Boolean success = AvontusPrincipal.Login("IT.Admin", "Scaffold");
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