﻿// System References
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;

// Internal Class references
using QuantifyWebAPI.Classes;
using System.Net.NetworkInformation;
using System.Net.Mail;

// Other References
using Newtonsoft.Json;


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
                                        'customer_id': 'Test111',
                                        'customer_name': 'TestCust111',
                                        'customer_phone': '612-867-5309',
                                        'customer_email': 'John@Smith.com',
                                        'customer_fax': '612-867-5309',

                                        'Address': [
                                        {
                                        'addressTypeCode': 'Business',
                                        'address1': 'One Main St',
                                        'address2': '',
                                        'city': 'Minneapolis',
                                        'state': 'MN',
                                        'zip': '55417',
                                        'country': 'USA'
                                        },
                                        {
                                        'addressTypeCode': 'Billing',
                                        'address1': 'Two Main St',
                                        'address2': '',
                                        'city': 'Minneapolis',
                                        'state': 'MN',
                                        'zip': '55417',
                                        'country': 'USA'
                                        }
                                        ],
                                        'Contact': [
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

            // Deserialize Json object to create class we can work with
            CustomerRootClass myDeserializedClass = JsonConvert.DeserializeObject<CustomerRootClass>(MyJSonResponse);

            // string mySerializedClass = JsonConvert.SerializeObject(myDeserializedClass);

            // Define fields
            string CustomerNumber = myDeserializedClass.CustomerData.customer_id;
            string CustomerName = myDeserializedClass.CustomerData.customer_name;
            string CustomerPhone = myDeserializedClass.CustomerData.customer_phone;
            string CustomerEmail = myDeserializedClass.CustomerData.customer_email;
            string CustomerFax = myDeserializedClass.CustomerData.customer_fax;


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

            // Instantiate response class for logging successes/errors if fail
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            // Validate and save the Customer record
            customerResponse = customerValidateAndSave(customer);


            // Update appropriate address information for Customer based on address type provided
            foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.CustomerData.Addresses)
            {
                // Re-fetch customer record each time we update address data
                customer = BusinessPartner.GetBusinessPartnerByNumber(myDeserializedClass.CustomerData.customer_id);
                if (myAddress.addressTypeCode == "Business")
                {
                        customer.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                        customer.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                        customer.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                        customer.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state;
                        customer.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                        customer.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                }
                else if (myAddress.addressTypeCode == "Billing")
                {
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state;
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                        customer.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                }
                else if (myAddress.addressTypeCode == "Shipping")
                {
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state;
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                        customer.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country; 
                }

                // Validate and save the Customer record
                customerResponse = customerValidateAndSave(customer);
            }
            // Serialize response class to Json to be passed back
            string customerResponseMessage = JsonConvert.SerializeObject(customerResponse);
            return customerResponseMessage;
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


        // Validates customer record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi.
        public CustomerResponseObj customerValidateAndSave(BusinessPartner parmCustomer)
        {
            // Create response object
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            // Create string list for errors
            List<string> errorList = new List<string>();

            if (parmCustomer.IsSavable)
            {
                try
                {
                    // Attempt to save
                    parmCustomer.Save();
                }
                catch (DataPortalException ex)
                {
                    // Pass back "F" for fail
                    customerResponse.status = "F";

                    // Get the object back from the data tier
                    parmCustomer = ex.BusinessObject as BusinessPartner;

                    // We can check to see if the name is unique
                    if (!parmCustomer.IsUnique)
                    {
                        // Fix the name
                    }
                    else
                    {
                        // Check the rules
                        foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
                        {
                            // Fix rules here, if you'd like
                            //if (rule.Property == "Name")
                            //{
                                // Fix the name here 
                            //}

                            // Log errors and pass back response 
                            errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                        }
                    }
                }
            }
            else
            {
                // Pass back "F" for fail
                customerResponse.status = "F";

                foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
                {
                    // Fix rules here, if you'd like
                    //if (rule.Property == "Name")
                    //{
                        // Fix the name here 
                    //}

                    // Log errors and pass back response 
                    errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                }

                customerResponse.errorList = errorList;
            }
            return customerResponse;
        }

    }
}
