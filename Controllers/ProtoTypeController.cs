using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

// Other References
using Newtonsoft.Json;

using QuantifyWebAPI.Classes;
using System.Text;

namespace QuantifyWebAPI.Controllers
{
    public class ProtoTypeController : ApiController
    {
        // GET: api/ProtoType
        public IEnumerable<string> GetArrayTest()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/ProtoType/5
        public string GetTest()
        {
            return "value";
        }

        public string GetTest2(int id)
        {
            return "value2";
        }

  

        // POST: api/ProtoType
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/ProtoType/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/ProtoType/5
        public void Delete(int id)
        {
        }

        /// <summary>  
        /// POST api/WebApi  
        /// </summary>  
        /// <param name="postData">Post data parameter</param>  
        /// <param name="request">Request parameter</param>  
        /// <returns>Return - Response</returns>  
        //[HttpPost]
        //public HttpResponseMessage UpsertCustomerData([FromBody] string JSonIn)
        //{
        //    // Initialization  
        //    HttpResponseMessage response = null;

        //    // Deserialize Json object to create class we can work with
        //    //CustomerRootClass myDeserializedClass = JsonConvert.DeserializeObject<CustomerRootClass>(JSonIn);

        //    //string mySerializedObject = JsonConvert.SerializeObject(myDeserializedClass);

        //    response = Request.CreateResponse(HttpStatusCode.OK);
        //    //response.Content = new StringContent("Test", Encoding.UTF8, "application/json");
        //    response.Content = new StringContent(JSonIn);

        //    return response;
        //}

        [HttpPost]
        public HttpResponseMessage UpsertCustomerData(string id)
        {
            // Initialization  
            HttpResponseMessage response = null;

            // Deserialize Json object to create class we can work with
            //CustomerRootClass myDeserializedClass = JsonConvert.DeserializeObject<CustomerRootClass>(JSonIn);

            //string mySerializedObject = JsonConvert.SerializeObject(myDeserializedClass);
            string myResponse = "";

            try
            {

                if (id.IndexOf("Test") > 0)
                {

                    myResponse = "Success";
                }
                else
                {
                    myResponse = "Fail";

                }
            }
            catch
            {
                myResponse = "We dont have Shit!";
            }

            response = Request.CreateResponse(HttpStatusCode.OK);
            //response.Content = new StringContent("Test", Encoding.UTF8, "application/json");
            response.Content = new StringContent(myResponse);

            return response;
        }
    }
}
