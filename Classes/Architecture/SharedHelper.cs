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
    public class SharedHelper
    {
        RaygunClient myRaygunClient = new RaygunClient();

        public string EvaluateProductXRef(Guid ProductID, string myProductID, string connectionString)
        /*
         * Connection settings are controlled by the regular Quantify Client. Please
         * start Quantify and verify you can log in. Once you can log in with
         * Quantify, the API will use the same connection strings
         */
        {
            //***** Get WebApps Product ID from Products XRef and assign as Product ID if it exists *****
            DAL myDAL = new DAL();
            DataTable myProductXRefRecord = myDAL.GetWebAppsIDProductsXRef(ProductID.ToString(), connectionString);
            string returnProductID;

            if (myProductXRefRecord.Rows.Count > 0)
            {
                string myWebAppsProductID = myProductXRefRecord.Rows[0]["WebAppsItemNumber"].ToString();
                if (myWebAppsProductID != null && myWebAppsProductID != "")
                {
                    returnProductID = myWebAppsProductID;
                }
                else
                {
                    returnProductID = myProductID;
                }
            }
            else
            {
                returnProductID = myProductID;
            }

            return returnProductID;
        }
    }
}