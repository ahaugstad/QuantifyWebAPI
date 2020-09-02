using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

// Other References
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantifyWebAPI.Controllers
{
    public class DataRouterPostController : ApiController
    {
        [HttpPost]
        public HttpResponseMessage UpsertDataObject(JObject jsonResult)
        {
            //***** Initialization *****
            HttpResponseMessage HttpResponse = null;
            string myResponse = "";

            string RequestType = jsonResult["entity"].ToString();

            switch (RequestType)
            {
                case "Customer":
                  
                    break;
        
                default:
                    Console.WriteLine("The color is unknown.");
                    break;
            }

            HttpResponse = Request.CreateResponse(HttpStatusCode.OK);
            HttpResponse.Content = new StringContent(myResponse);

            return HttpResponse;
        }
}
