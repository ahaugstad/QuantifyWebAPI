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
        QuantifyHelper QuantHelper;
        string initializationMode;
        public CustomerBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public string UpsertCustomerData(JObject jsonResult)
        {
            //***** Initialization *****
            string myResponse = "Success";
            
            //***** Instantiate response class for logging successes/errors if fail ***** 
            CustomerResponseObj CustomerResponse = new CustomerResponseObj();

            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Log on to Quantify *****
                QuantHelper.QuantifyLogin();

                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass = jsonResult.ToObject<CustomerRootClass>();


                //***** Define fields *****
                string CustomerNumber = myDeserializedClass.CustomerData.customer_id;
                string CustomerName = myDeserializedClass.CustomerData.customer_name;
                string CustomerPhone = myDeserializedClass.CustomerData.customer_phone;
                string CustomerEmail = myDeserializedClass.CustomerData.customer_email;
                string CustomerFax = myDeserializedClass.CustomerData.customer_fax;


                //*****  Instantiate Customer we are inserting/updating; check if it already exists before updating/inserting ***** 
                BusinessPartner Customer = BusinessPartner.GetBusinessPartnerByAccountingID(CustomerNumber);

                //****** check to See if Create or Update *****
                if (Customer.PartnerNumber == "")
                {
                    //***** Create new Customer ***** 
                    Customer = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                }
                else
                {
                    //***** Get existing Customer ***** 
                    Customer = BusinessPartner.GetBusinessPartnerByAccountingID(CustomerNumber);
                }

                //***** Set non-address Customer fields ***** 
                Customer.AccountingID = CustomerNumber;
                Customer.Name = CustomerName;
                Customer.PhoneNumber = CustomerPhone;
                Customer.EmailAddress = CustomerEmail;
                Customer.FaxNumber = CustomerFax;

                //***** Validate and save the Customer record ***** 
                CustomerResponse = CustomerValidateAndSave(Customer);

                //***** Verify we have successfully saved Customer record's non-address fields before moving on to addresses - if not, skip and return errors
                if (CustomerResponse.status != "Error")
                {
                    //***** Update appropriate address information for Customer based on address type provided ***** 
                    foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.CustomerData.Addresses)
                    {
                        //***** Get state object for updating State ID below *****
                        //TODO: ADH 10/12/2020 - TEST: Below works and new exception looks good in Raygun
                        State state = State.NewState();
                        try
                        {
                            state = State.GetState(myAddress.state);
                        }
                        catch (Avontus.Core.DataPortalException ex)
                        {
                            //***** Create Raygun Exception Package *****
                            RaygunExceptionPackage myRaygunStateErrorPackage = new RaygunExceptionPackage();
                            myRaygunStateErrorPackage.Tags.Add("Data Portal");
                            myRaygunStateErrorPackage.Tags.Add("Customer");
                            myRaygunStateErrorPackage.Tags.Add("Configuration Error");
                            myRaygunStateErrorPackage.CustomData.Add("WebApps Customer ID: ", Customer.AccountingID);
                            Exception myStateException = new Exception("State code " + myAddress.state + " does not exist in Quantify", ex);
                            myRaygunClient.SendInBackground(myStateException, myRaygunStateErrorPackage.Tags, myRaygunStateErrorPackage.CustomData);
                            throw myStateException;
                        }

                        //***** Re-fetch Customer record each time we update address data ***** 
                        Customer = BusinessPartner.GetBusinessPartnerByAccountingID(myDeserializedClass.CustomerData.customer_id);
                        if (myAddress.addressTypeCode == "Business" || myAddress.addressTypeCode == null || myAddress.addressTypeCode == "")
                        {
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state.ToUpper();
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Billing")
                        {
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state.ToUpper();
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                            Customer.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Shipping")
                        {
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                            Customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state.ToUpper();
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                            Customer.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                        }

                        //***** Validate and save the Customer record ***** 
                        CustomerResponse = CustomerValidateAndSave(Customer);
                        if (CustomerResponse.status == "Error") { break; }
                    }
                }
            }
            catch (SqlException ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunSQLErrorPackage = new RaygunExceptionPackage();
                myRaygunSQLErrorPackage.Tags.Add("Generic");
                myRaygunSQLErrorPackage.Tags.Add("Customer");
                myRaygunClient.SendInBackground(ex, myRaygunSQLErrorPackage.Tags);

                CustomerResponse.status = "SQL Exception Error";
                CustomerResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + ex.Message.ToString());
            }
            catch (Exception ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunGenericErrorPackage = new RaygunExceptionPackage();
                myRaygunGenericErrorPackage.Tags.Add("Generic");
                myRaygunGenericErrorPackage.Tags.Add("Customer");
                myRaygunClient.SendInBackground(ex, myRaygunGenericErrorPackage.Tags);

                CustomerResponse.status = "Error";
                CustomerResponse.errorList.Add("Generic error - " + Environment.NewLine + ex.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(CustomerResponse);

            return myResponse;
        }


        //***** Validates Customer record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
        public CustomerResponseObj CustomerValidateAndSave(BusinessPartner parmCustomer)
        {
            //***** Create response object ***** 
            CustomerResponseObj CustomerResponse = new CustomerResponseObj();

            //***** Create string list for errors ***** 
            List<string> errorList = new List<string>();

            try
            {
                //***** Attempt to save ***** 
                parmCustomer.Save();
                CustomerResponse.status = "Success";
            }
            catch (DataPortalException ex)
            {
                //TODO: ADH 10/12/2020 - TEST: All new Raygun exception handling works below
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunDataPortalPackage = new RaygunExceptionPackage();
                myRaygunDataPortalPackage.Tags.Add("Data Portal");
                myRaygunDataPortalPackage.Tags.Add("Customer");
                myRaygunDataPortalPackage.CustomData.Add("WebApps Customer ID", parmCustomer.AccountingID);
                myRaygunClient.SendInBackground(ex, myRaygunDataPortalPackage.Tags, myRaygunDataPortalPackage.CustomData);

                //***** Pass back "Error" for fail ***** 
                CustomerResponse.status = "Error";

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
                        //***** Create Raygun Exception Package *****
                        RaygunExceptionPackage myRaygunValidationPackage = new RaygunExceptionPackage();
                        myRaygunValidationPackage.Tags.Add("Validation");
                        myRaygunValidationPackage.Tags.Add("Customer");
                        myRaygunValidationPackage.CustomData.Add("WebApps Customer ID", parmCustomer.AccountingID);
                        Exception myValidationEx = new Exception("Validation error: " + rule.RuleName + " - " + rule.Description);
                        myRaygunClient.SendInBackground(myValidationEx, myRaygunValidationPackage.Tags, myRaygunValidationPackage.CustomData);

                        //***** Log errors and pass back response  ***** 
                        errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                    }
                    CustomerResponse.errorList = errorList;
                }
            }
            return CustomerResponse;
        }
    }
}
