// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

// Other References
using Newtonsoft.Json;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;
using QuantifyWebAPI.Classes;




namespace QuantifyWebAPI.Controllers
{
    public class HomeController : Controller
    {
        /*
         * Default Methods
         */
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        public ActionResult Alex()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        /*
         * Test Methods
         */
        public async Task<string> TestConnection()
        {

            string MyJSonResponse = @"{
                                        'entity': 'Customer',
                                        'CustomerData': {
                                        'customer_id': '1',
                                        'customer_name': '1',
                                        'customer_phone': '1025',
                                        'customer_email': 'Jane',
                                        'customer_fax': 'Smith',

                                        'Addresses': [
                                        {
                                        'addressTypeCode': 'Primary',
                                        'address1': 'One Main St',
                                        'address2': '',
                                        'city': 'Minneapolis',
                                        'state': 'MN',
                                        'zip': '55417'
                                        },
                                        {
                                        'addressTypeCode': 'Mailing',
                                        'address1': 'One Main St',
                                        'address2': '',
                                        'city': 'Minneapolis',
                                        'state': 'MN',
                                        'zip': '55417'
                                        }
                                        ],
                                        'Contacts': [
                                        {
                                        'contact_name': 'Primary',
                                        'contact_phone': 'One Main St',
                                        'contact_email': ''
                                        },
                                        {
                                        'contact_name': 'Primary',
                                        'contact_phone': 'One Main St',
                                        'contact_email': ''
                                        }
                                        ]
                                        }}";

            //CustomerRootClass MyCustomer = JsonSerializer.Deserialize<CustomerRootClass>(MyJSonResponse);
            CustomerRootClass myDeserializedClass = JsonConvert.DeserializeObject<CustomerRootClass>(MyJSonResponse);
            return "S";
        }

        /*
         * Quantify API methods
         */
        // Update Customer Record
        public async Task<JsonResult> UpdateCustomer()
        {
            QuantifyLogin();
            // Get list of all customers
            //BusinessPartner customer = BusinessPartner.GetBusinessPartner(CustomerID);
            
            var result = new JsonResult();

            return result;
        }

        // GET Quantify Jobs
        // **Need to implement ability to return JSON file
        public string GetJobs(byte VersionStamp)
        {
            QuantifyLogin();
            // Get list of all jobsites
            StockingLocationList jobs = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);
            // Filter down to only active jobs that have been updated since the last run date
            /*
              StockingLocationList activejobs = from job in jobs
                         where job.IsActive
                         select job.Number, 
                         job.Description, job.Name, job.Street, job.City, job.State, job.PostalCode, job.CustomerNumber, job.ConsumablesEnabled, job.CycleBeginDateTime, job.CycleEndDateTime
            */
            return "value";
        }

        // Quantify Login method
        public void QuantifyLogin()
        /*
         * Connection settings are controlled by the regular Quantify Client. Please
         * start Quantify and verify you can log in. Once you can log in with
         * Quantify, the API will use the same connection strings
         */
        {
            AvontusPrincipal.Logout();
            Boolean success = AvontusPrincipal.Login("admin", "password");
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
