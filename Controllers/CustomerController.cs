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


// Internal Class referances
using QuantifyWebAPI.Classes;

namespace QuantifyWebAPI.Controllers
{
    public class CustomerController : ApiController
    {
        // GET: api/Customer
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Customer/5
        public string Get(int id)
        {
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            string MyJSonResponse = @"{
                                        'entity': 'Customer',
                                        'CustomerData': {
                                        'customer_id': 'Test123',
                                        'customer_name': 'TestCust123',
                                        'customer_phone': '612-867-5309',
                                        'customer_email': 'John@Smith.com',
                                        'customer_fax': '612-867-5309',

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

            // Deserialize Json object to create class we can work with
            CustomerRootClass myDeserializedClass = JsonConvert.DeserializeObject<CustomerRootClass>(MyJSonResponse);

            // Define fields and convert necessary for use with API
            // Guid PartnerID = Guid.Parse(myDeserializedClass.CustomerData.customer_id);
            string CustomerNumber = myDeserializedClass.CustomerData.customer_id;
            string CustomerName = myDeserializedClass.CustomerData.customer_name;
            string CustomerPhone = myDeserializedClass.CustomerData.customer_phone;
            string CustomerEmail = myDeserializedClass.CustomerData.customer_email;
            string CustomerFax = myDeserializedClass.CustomerData.customer_fax;
            //string CustomerPrimaryAddress = myDeserializedClass.CustomerData.Addresses.Find(x => x.addressTypeCode == "Primary");

            // Instantiate customer we are inserting/updating; check if it already exists before updating/inserting
            BusinessPartner customer;
            //if(IsNull(BusinessPartner.GetBusinessPartnerByNumber(CustomerNumber)))
            if (CustomerName != "TestCust123")
            {
                // Create new customer
                customer = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                customer.PartnerNumber = CustomerNumber;
            }
            else
            {
                // Get existing customer
                customer = BusinessPartner.GetBusinessPartnerByNumber(CustomerNumber);
            }

            // Set general customer fields
            customer.AccountingID = CustomerNumber;
            customer.Name = CustomerName;
            customer.PhoneNumber = CustomerPhone;
            customer.EmailAddress = CustomerEmail;
            customer.FaxNumber = CustomerFax;

            bool isDirty = customer.IsDirty;

            if (customer.IsSavable)
            {
                try
                {
                    // Attempt to save
                    customer.Save();
                }
                catch (DataPortalException ex)
                {
                    // Get the object back from the data tier
                    customer = ex.BusinessObject as BusinessPartner;

                    // We can check to see if the name is unique
                    if (!customer.IsUnique)
                    {
                        // Fix the name
                    }
                    else
                    {
                        // Check the rules
                    }
                }
            }

            else
            {
                foreach (Avontus.Core.Validation.BrokenRule rule in customer.BrokenRulesCollection)
                {
                    if (rule.Property == "Name")
                    {
                        // Fix the name here 
                    }
                }
            }
            return "S";
        }

        // POST: api/Customer
        public void Post([FromBody]string value)
        {
            QuantifyHelper QuantHelper = new QuantifyHelper();

            QuantHelper.QuantifyLogin();

            // Get list of all customers
            //BusinessPartner customer = BusinessPartner.GetBusinessPartner(CustomerID);

            //var result = new JsonResult();

            //return result;
        }

        // PUT: api/Customer/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Customer/5
        public void Delete(int id)
        {
        }
    }
}
