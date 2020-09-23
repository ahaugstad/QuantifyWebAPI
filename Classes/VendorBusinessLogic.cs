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
    public class VendorBusinessLogic
    {
        RaygunClient myRaygunClient = new RaygunClient();
        QuantifyHelper QuantHelper;
        public VendorBusinessLogic(QuantifyCredentials QuantCreds)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
        }

        public string UpsertVendorData(JObject jsonResult)
        {
            //***** Initialization *****
            string myResponse = "Success";

            //***** Instantiate response class for logging successes/errors if fail ***** 
            VendorResponseObj VendorResponse = new VendorResponseObj();



            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Log on to Quantify *****
                QuantHelper.QuantifyLogin();

                //***** Deserialize JObject to create class we can work with ******
                VendorRootClass myDeserializedClass = jsonResult.ToObject<VendorRootClass>();


                //***** Define fields *****
                string VendorNumber = myDeserializedClass.VendorData.vendor_id;
                string VendorName = myDeserializedClass.VendorData.vendor_name;
                string VendorPhone = myDeserializedClass.VendorData.vendor_phone;
                string VendorEmail = myDeserializedClass.VendorData.vendor_email;
                //TODO: ADH 9/1/2020 - BUSINESS DECISION
                // QuantifyWebAPI.Classes.Contact myContact = myDeserializedClass.CustomerData.Contact[0];
                // string CustomerEmail2 = myContact.contact_email;
                string VendorFax = myDeserializedClass.VendorData.vendor_fax;
                //string VendorIsActive = myDeserializedClass.VendorData.is_active;


                //*****  Instantiate customer we are inserting/updating; check if it already exists before updating/inserting ***** 
                BusinessPartner Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(VendorNumber);

                //****** check to See if Create or Update *****
                if (Vendor.PartnerNumber == "")
                {
                    //***** Create new customer ***** 
                    Vendor = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                    //TODO: ADH 9/2/2020 - BUSINESS DECISION
                    // See if the following line is what we want to do, since there is auto-numbering conventions in Quantify. 
                    // We always have the AccountingID field to sync a record up with WebApps if need be.
                    // If we uncomment this, we need to change all 'GetBusinessPartnerByAccountingID' calls to 'GetBusinessPartnerByNumber'
                    //customer.PartnerNumber = CustomerNumber;
                }
                else
                {
                    //***** Get existing customer ***** 
                    Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(VendorNumber);
                }

                //***** Set non-address customer fields ***** 
                Vendor.AccountingID = VendorNumber;
                Vendor.Name = VendorName;
                Vendor.PhoneNumber = VendorPhone;
                Vendor.EmailAddress = VendorEmail;
                Vendor.FaxNumber = VendorFax;
                //if (CustomerIsActive == "A") { customer.IsActive = true; } else { customer.IsActive = false; }

                //***** Validate and save the Customer record ***** 
                VendorResponse = VendorValidateAndSave(Vendor);

                //***** Verify we have successfully saved Customer record's non-address fields before moving on to addresses - if not, skip and return errors
                if (VendorResponse.status != "Error")
                {
                    //***** Update appropriate address information for Customer based on address type provided ***** 
                    foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.VendorData.Addresses)
                    {
                        //***** Get state object for updating State ID below *****
                        State state = State.GetState(myAddress.state);

                        //***** Re-fetch customer record each time we update address data ***** 
                        Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(myDeserializedClass.VendorData.vendor_id);
                        if (myAddress.addressTypeCode == "Business" || myAddress.addressTypeCode == null || myAddress.addressTypeCode == "")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
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
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Billing")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Shipping")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                        }

                        //***** Validate and save the Customer record ***** 
                        VendorResponse = VendorValidateAndSave(Vendor);
                        if (VendorResponse.status == "Error") { break; }
                    }
                }
            }
            catch (SqlException ex)
            {
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                VendorResponse.status = "SQL Exception Error";
                VendorResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + ex.Message.ToString());
            }
            catch (Exception ex)
            {
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                VendorResponse.status = "Error";
                VendorResponse.errorList.Add("Generic error - " + Environment.NewLine + ex.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(VendorResponse);

            return myResponse;
        }


        //***** Validates customer record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
        public VendorResponseObj VendorValidateAndSave(BusinessPartner parmVendor)
        {
            //***** Create response object ***** 
            VendorResponseObj VendorResponse = new VendorResponseObj();

            //***** Create string list for errors ***** 
            List<string> errorList = new List<string>();

            try
            {
                //***** Attempt to save ***** 
                parmVendor.Save();
            }
            catch (DataPortalException ex)
            {
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                //***** Pass back "Error" for fail ***** 
                VendorResponse.status = "Error";

                //***** Get the object back from the data tier ***** 
                parmVendor = ex.BusinessObject as BusinessPartner;

                //***** We can check to see if the name is unique ***** 
                if (!parmVendor.IsUnique)
                {
                    //***** Fix the name ***** 
                }
                else
                {
                    //***** Check the rules ***** 
                    foreach (Avontus.Core.Validation.BrokenRule rule in parmVendor.BrokenRulesCollection)
                    {
                        //***** Fix rules here, if you'd like ***** 
                        //if (rule.Property == "Name")
                        //{
                        //***** Fix the name here  ***** 
                        //}

                        //***** Log errors and pass back response  ***** 
                        errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                    }
                    VendorResponse.errorList = errorList;
                }
            }
            return VendorResponse;
        }
    }
}
