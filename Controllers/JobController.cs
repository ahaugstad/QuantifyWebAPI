using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

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

// Internal Class referances
using QuantifyWebAPI.Classes;

namespace QuantifyWebAPI.Controllers
{
    public class JobController : ApiController
    {
        // GET: api/Jobs
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Jobs/5
        public string Get(byte parmVersionStamp)
        {
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            // Get list of all jobsites
            StockingLocationList jobsites = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);

            // Filter down to only active jobs that have been updated since the last run date
            foreach (StockingLocationListItem jobsiteItem in jobsites)
            {
                StockingLocation jobsite = StockingLocation.GetStockingLocation(jobsiteItem.StockingLocationID, false);
                if(jobsite.VersionStamp[jobsite.VersionStamp.Length] != parmVersionStamp)
                {

                }
            }

            return "value";
        }

        // POST: api/Jobs
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Jobs/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Jobs/5
        public void Delete(int id)
        {
        }
    }
}
