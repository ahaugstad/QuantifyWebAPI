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

        #region Fiddler Prototypes 
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


        [HttpPost]
        public HttpResponseMessage UpsertCustomerData(JObject jsonResult)
        {
            // Initialization  
            HttpResponseMessage response = null;
            string myResponse = "";
            string JSonIn = "";
            try
            {
                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass =  jsonResult.ToObject<CustomerRootClass>();


                myResponse = JsonConvert.SerializeObject(myDeserializedClass);
            }
            catch
            {
                myResponse = "JSON received Can not be converted to associated .net Class.";
            }

            

            response = Request.CreateResponse(HttpStatusCode.OK);
            //response.Content = new StringContent("Test", Encoding.UTF8, "application/json");
            response.Content = new StringContent(myResponse);

            return response;
        }

        #endregion

        #region Template for Upsert
        [HttpPost]
        public HttpResponseMessage UpsertTemplateData(JObject jsonResult)
        {
            // Initialization  
            HttpResponseMessage HttpResponse = null;
            string myResponse = "";

            //***** Log on to Quantify *****
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();


            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass = jsonResult.ToObject<CustomerRootClass>();


                //***** Define fields *****
     


                //*****  Instantiate Quantify Object we are inserting/updating; check if it already exists before updating/inserting ***** 
                //BusinessPartner customer;

                //****** check to See if Create or Update *****
                //if (CustomerName != "TestCust123")
                //{
                //    //***** Create new customer ***** 
                //    customer = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                //    customer.PartnerNumber = CustomerNumber;
                //}
                //else
                //{
                    //***** Get existing customer ***** 
                //    customer = BusinessPartner.GetBusinessPartnerByNumber(CustomerNumber);
                //}

                //***** Set general customer fields ***** 
            

                //***** Instantiate response class for logging successes/errors if fail ***** 
                CustomerResponseObj customerResponse = new CustomerResponseObj();

                //***** Validate and save the Object record ***** 
                //customerResponse = customerValidateAndSave(customer);

                //***** Update appropriate address information for Customer based on address type provided ***** 
                foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.CustomerData.Addresses)
                {
                    //***** Re-fetch customer record each time we update address data ***** 
                    //customer = BusinessPartner.GetBusinessPartnerByNumber(myDeserializedClass.CustomerData.customer_id);
                    //if (myAddress.addressTypeCode == "Business")
                    //{
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                    //}
                    //else if (myAddress.addressTypeCode == "Billing")
                    //{
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                    //}
                    //else if (myAddress.addressTypeCode == "Shipping")
                    //{
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                    //    customer.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                    //}

                    //***** Validate and save the Object record ***** 
                    //customerResponse = customerValidateAndSave(customer);
                }

                //***** Serialize response class to Json to be passed back ******
                myResponse = JsonConvert.SerializeObject(customerResponse);

            }
            catch
            {
                myResponse = "JSON received Can not be converted to associated .net Class and/or saved in Quantify.";
            }

            HttpResponse = Request.CreateResponse(HttpStatusCode.OK);
            //response.Content = new StringContent("Test", Encoding.UTF8, "application/json");
            HttpResponse.Content = new StringContent(myResponse);

            return HttpResponse;
        }
    #endregion
    }
}
