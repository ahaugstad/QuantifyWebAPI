// System References
using System;
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
using System.Configuration;

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

namespace QuantifyWebAPI.Controllers
{

    public class DataRouterPostController : ApiController
    {
        String StrVersionDBConn = ConfigurationManager.AppSettings["QuantifyPersistanceLayerDBConn"];
        RaygunClient myRaygunClient = new RaygunClient();

        //***** Boomi pings this method on schedule to kick off our processing of all Quantify-outbound integrations *****
        [HttpGet]
        public void PingInitialization()
        {
            //***** Run Jobs *****
            JobBusinessLogic myJobResponse = new JobBusinessLogic();
            myJobResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Run Products *****
            ProductBusinessLogic myProductResponse = new ProductBusinessLogic();
            myProductResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Run Inventory Transactions *****
            InventoryTransBusinessLogic myInventoryTransResponse = new InventoryTransBusinessLogic();
            myInventoryTransResponse.GetIDsToProcess(StrVersionDBConn);
        }

        //***** This method is called anytime a Quantify-inbound request comes in from Boomi *****
        [HttpPost]
        public HttpResponseMessage UpsertDataObject(JObject jsonResult)
        {
            //***** Initialization *****
            HttpResponseMessage HttpResponse = null;
            string myResponse = "Sucess";

            string RequestType = jsonResult["entity"].ToString();
            try
            {
                switch (RequestType)
                {
                    case "Customer":
                        CustomerBusinessLogic myCustomerResponse = new CustomerBusinessLogic();
                        myResponse = myCustomerResponse.UpsertCustomerData(jsonResult);
                        break;

                    default:
                        throw new System.ArgumentException("Unknown 'Request Type' Submitted in UpsertDataObject", RequestType);
                }

                HttpResponse = Request.CreateResponse(HttpStatusCode.OK);
                HttpResponse.Content = new StringContent(myResponse);
            }
            catch (Exception ex)
            {
                myRaygunClient.SendInBackground(ex);
                HttpResponse = Request.CreateResponse(HttpStatusCode.OK);
                //ToDo:ERC 9/7/2020 Possibly Need to make Repsonse Class
                HttpResponse.Content = new StringContent("Failed");
            }
            return HttpResponse;
        }
    }
}
