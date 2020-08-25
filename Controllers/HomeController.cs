// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;

namespace QuantifyWebAPI.Controllers
{
    public class HomeController : Controller
    {
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
         * Quantify API methods
         */
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
