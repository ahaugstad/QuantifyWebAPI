// System References
using System;
using System.Reflection;
using System.IO;
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
    public class CustomerBusinessLogic
    {
        RaygunClient myRaygunClient = new RaygunClient();
        
    public string UpsertCustomerData(JObject jsonResult)
        {
            //***** Initialization *****
            string myResponse = "";
            
            //***** Instantiate response class for logging successes/errors if fail ***** 
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Log on to Quantify *****
                QuantifyHelper QuantHelper = new QuantifyHelper();

                QuantHelper.QuantifyLogin();

                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass = jsonResult.ToObject<CustomerRootClass>();


                //***** Define fields *****
                string CustomerNumber = myDeserializedClass.CustomerData.customer_id;
                string CustomerName = myDeserializedClass.CustomerData.customer_name;
                string CustomerPhone = myDeserializedClass.CustomerData.customer_phone;
                string CustomerEmail = myDeserializedClass.CustomerData.customer_email;
                //TODO: ADH 9/1/2020 - BUSINESS DECISION
                // QuantifyWebAPI.Classes.Contact myContact = myDeserializedClass.CustomerData.Contact[0];
                // string CustomerEmail2 = myContact.contact_email;
                string CustomerFax = myDeserializedClass.CustomerData.customer_fax;
                string CustomerIsActive = myDeserializedClass.CustomerData.is_active;


                //*****  Instantiate customer we are inserting/updating; check if it already exists before updating/inserting ***** 
                BusinessPartner customer = BusinessPartner.GetBusinessPartnerByAccountingID(CustomerNumber);

                //****** check to See if Create or Update *****
                if (customer.PartnerNumber == "")
                {
                    //***** Create new customer ***** 
                    customer = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                    //TODO: ADH 9/2/2020 - BUSINESS DECISION
                    // See if the following line is what we want to do, since there is auto-numbering conventions in Quantify. 
                    // We always have the AccountingID field to sync a record up with WebApps if need be.
                    // If we uncomment this, we need to change all 'GetBusinessPartnerByAccountingID' calls to 'GetBusinessPartnerByNumber'
                    //customer.PartnerNumber = CustomerNumber;
                }
                else
                {
                    //***** Get existing customer ***** 
                    customer = BusinessPartner.GetBusinessPartnerByAccountingID(CustomerNumber);
                }

                //***** Set non-address customer fields ***** 
                customer.AccountingID = CustomerNumber;
                customer.Name = CustomerName;
                customer.PhoneNumber = CustomerPhone;
                customer.EmailAddress = CustomerEmail;
                customer.FaxNumber = CustomerFax;
                if (CustomerIsActive == "A") { customer.IsActive = true; } else { customer.IsActive = false; }

                //***** Validate and save the Customer record ***** 
                customerResponse = customerValidateAndSave(customer);

                //***** Verify we have successfully saved Customer record's non-address fields before moving on to addresses - if not, skip and return errors
                if (customerResponse.status != "Error")
                {
                    //***** Update appropriate address information for Customer based on address type provided ***** 
                    foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.CustomerData.Addresses)
                    {
                        //***** Get state object for updating State ID below *****
                        State state = State.GetState(myAddress.state);

                        //***** Re-fetch customer record each time we update address data ***** 
                        customer = BusinessPartner.GetBusinessPartnerByAccountingID(myDeserializedClass.CustomerData.customer_id);
                        if (myAddress.addressTypeCode == "Business" || myAddress.addressTypeCode == null || myAddress.addressTypeCode == "")
                        {
                            customer.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                            //TODO: ADH 9/1/2020 - BUSINESS DECISION
                            // Should we be adding in non-existent states through code? Or should we throw an error and have users configure it in Quantify UI? 
                            // Check if State exists - if it doesn't, first need to add it, before passing GUID into State ID *****
                            //if (state.StateID == Guid.Empty)
                            //{
                            //    State newState = State.NewState();
                            //    newState.Name = myAddress.state;
                            //    newState.Save();
                            //    customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = newState.StateID;
                            //}
                            //else
                            //{
                            //    customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            //}
                            customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state.ToUpper();
                            customer.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Billing")
                        {
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state.ToUpper();
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                            customer.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Shipping")
                        {
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                            customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state.ToUpper();
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                            customer.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                        }

                        //***** Validate and save the Customer record ***** 
                        customerResponse = customerValidateAndSave(customer);
                        if (customerResponse.status != "Error") { customerResponse.status = "Success"; } else { break; }
                    }
                }
            }
            catch (SqlException ex)
            {
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                customerResponse.status = "SQL Exception Error";
                customerResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + ex.Message.ToString());
            }
            catch (Exception ex)
            {
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                customerResponse.status = "Error";
                customerResponse.errorList.Add("Generic error - " + Environment.NewLine + ex.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(customerResponse);

            return myResponse;
        }


        //***** Validates customer record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
        public CustomerResponseObj customerValidateAndSave(BusinessPartner parmCustomer)
        {
            //***** Create response object ***** 
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            //***** Create string list for errors ***** 
            List<string> errorList = new List<string>();

            if (parmCustomer.IsSavable)
            {
                try
                {
                    //***** Attempt to save ***** 
                    parmCustomer.Save();
                }
                catch (DataPortalException ex)
                {
                    //***** log the error ******
                    myRaygunClient.SendInBackground(ex);

                    //***** Pass back "Error" for fail ***** 
                    customerResponse.status = "Error";

                    //***** Get the object back from the data tier ***** 
                    parmCustomer = ex.BusinessObject as BusinessPartner;

                    //***** We can check to see if the name is unique ***** 
                    if (!parmCustomer.IsUnique)
                    {
                        //***** Fix the name ***** 
                    }
                    else
                    {
                        //***** Check the rules ***** 
                        foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
                        {
                            //***** Fix rules here, if you'd like ***** 
                            //if (rule.Property == "Name")
                            //{
                            //***** Fix the name here  ***** 
                            //}

                            //***** Log errors and pass back response  ***** 
                            errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                        }
                    }
                }
            }
            else
            {
                //***** Pass back "Error" for fail ***** 
                customerResponse.status = "Error";

                foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
                {
                    //***** Fix rules here, if you'd like ***** 
                    //if (rule.Property == "Name")
                    //{
                    //***** Fix the name here  ***** 
                    //}

                    //***** Log errors and pass back response  ***** 
                    errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                }

                customerResponse.errorList = errorList;
            }
            return customerResponse;
        }
    }
}
