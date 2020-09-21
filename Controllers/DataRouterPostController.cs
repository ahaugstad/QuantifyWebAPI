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
        BoomiHelper BoomiHelper = new BoomiHelper();

        String StrVersionDBConn = ConfigurationManager.AppSettings["QuantifyPersistanceLayerDBConn"];
        RaygunClient myRaygunClient = new RaygunClient();

        String StrQuantifyUser = ConfigurationManager.AppSettings["QuantifyCredentials"];        
        QuantifyCredentials myQuantifyCredentials;

       
        public DataRouterPostController()
        {
            string[] MyQuantCredArray = StrQuantifyUser.Split('|');
            myQuantifyCredentials = new QuantifyCredentials(MyQuantCredArray[0], MyQuantCredArray[1]);

        }

        //***** Boomi pings this method on schedule to kick off our processing of all Quantify-outbound integrations *****
        [HttpGet]
        public void PingInitialization()
        {
            //***** Run Jobs *****
            //JobBusinessLogic myJobResponse = new JobBusinessLogic();
            //myJobResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Run Products *****
            //ProductBusinessLogic myProductResponse = new ProductBusinessLogic(myQuantifyCredentials);
            //myProductResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Run Inventory Transactions *****
            InventoryTransBusinessLogic myInventoryTransResponse = new InventoryTransBusinessLogic(myQuantifyCredentials);
            myInventoryTransResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Run Purchase Order Transactions *****
            PurchaseOrderBusinessLogic myPurchaseOrderResponse = new PurchaseOrderBusinessLogic(myQuantifyCredentials);
            myPurchaseOrderResponse.GetIDsToProcess(StrVersionDBConn);

            //***** Call Boomi to kick off processing *****
            BoomiHelper.PostBoomiAPI();
        }

        [HttpGet]
        public string ReprocessAuditLogErrorByEntityAndQuantifyID(string Entity, string QuantifyID)
        {
            //***** Use the following Link to Test Passing Parameters via Get to Execute Controller.    *****
            //*****         Change parameters to match your test.                                       *****                                                                      
            //***** https://localhost:44387/api/DataRouterPost/ReprocessAuditLogErrorByEntityAndQuantifyID?Entity=test&QuantifyID=Junk *****
            DataTable myDalResponse = new DataTable();
            DAL myDAL = new DAL();
            myDalResponse = myDAL.ReprocessAuditLogErrorByEntityAndQuantifyID(Entity, QuantifyID, StrVersionDBConn);

            return myDalResponse.Rows[0]["Status"].ToString();
        }

        [HttpGet]
        public string ReprocessAuditLogErrorByEntity(string Entity)
        {
            //***** Use the following Link to Test Passing the Entity Parameter via Get to Execute Controller.  *****
            //*****         Change parameters to match your test.                                               *****  
            //***** https://localhost:44387/api/DataRouterPost/ReprocessAuditLogErrorByEntity?Entity=Jobs       *****
            DataTable myDalResponse = new DataTable();
            DAL myDAL = new DAL();
            myDalResponse = myDAL.ReprocessAuditLogErrorByEntity(Entity,  StrVersionDBConn);

            return myDalResponse.Rows[0]["Status"].ToString();
        }

        [HttpGet]
        public string RemoveOldQuantifyLogRecords(string ProcessStatus)
        {
            //***** Use the following Link to Test Passing the ProcessStatus Parameter via Get to Execute       *****
            //*****         Controller. Change parameters to match your test.                                               *****
            //***** https://localhost:44387/api/DataRouterPost/ReprocessAuditLogErrorByEntity?ProcessStatus=P   *****
            DataTable myDalResponse = new DataTable();
            DAL myDAL = new DAL();
            myDalResponse = myDAL.RemoveOldQuantifyLogRecords(ProcessStatus,  StrVersionDBConn);

            return myDalResponse.Rows[0]["Status"].ToString();
        }

        //***** This method is called anytime a Quantify-inbound request comes in from Boomi *****
        [HttpPost]
        public HttpResponseMessage UpsertDataObject(JObject jsonResult)
        {
            //***** Initialization *****
            HttpResponseMessage HttpResponse = null;
            string myResponse = "Success";

            string RequestType = jsonResult["entity"].ToString();
            try
            {
                switch (RequestType)
                {
                    case "Customer":
                        CustomerBusinessLogic myCustomerResponse = new CustomerBusinessLogic(myQuantifyCredentials);
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
