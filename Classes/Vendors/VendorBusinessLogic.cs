﻿// System References
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
        string initializationMode;

        public VendorBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
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
                string VendorFax = myDeserializedClass.VendorData.vendor_fax;


                //*****  Instantiate Vendor we are inserting/updating; check if it already exists before updating/inserting ***** 
                BusinessPartner Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(VendorNumber);

                //****** check to See if Create or Update *****
                if (Vendor.PartnerNumber == "")
                {
                    //***** Create new Vendor ***** 
                    Vendor = BusinessPartner.NewBusinessPartner(PartnerTypes.Vendor);
                }
                else
                {
                    //***** Get existing Vendor ***** 
                    Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(VendorNumber);
                }

                //***** Set non-address customer fields ***** 
                Vendor.AccountingID = VendorNumber;
                Vendor.Name = VendorName;
                Vendor.PhoneNumber = VendorPhone;
                Vendor.EmailAddress = VendorEmail;
                Vendor.FaxNumber = VendorFax;

                //***** Validate and save the Vendor record ***** 
                VendorResponse = VendorValidateAndSave(Vendor);

                //***** Verify we have successfully saved Vendor record's non-address fields before moving on to addresses - if not, skip and return errors
                if (VendorResponse.status != "Error")
                {
                    //***** Update appropriate address information for Vendor based on address type provided ***** 
                    foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.VendorData.Addresses)
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
                            myRaygunStateErrorPackage.Tags.Add("Vendor");
                            myRaygunStateErrorPackage.Tags.Add("Configuration Error");
                            myRaygunStateErrorPackage.CustomData.Add("WebApps Vendor ID: ", Vendor.AccountingID);
                            Exception myStateException = new Exception("State code " + myAddress.state + " does not exist in Quantify", ex);
                            myRaygunClient.SendInBackground(myStateException, myRaygunStateErrorPackage.Tags, myRaygunStateErrorPackage.CustomData);
                            throw myStateException;
                        }

                        //***** Re-fetch Vendor record each time we update address data ***** 
                        Vendor = BusinessPartner.GetBusinessPartnerByAccountingID(myDeserializedClass.VendorData.vendor_id);
                        if (myAddress.addressTypeCode == "Billing" || myAddress.addressTypeCode == null || myAddress.addressTypeCode == "")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Shipping")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                        }
                        else if (myAddress.addressTypeCode == "Business")
                        {
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state.ToUpper();
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                            Vendor.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                        }

                        //***** Validate and save the Vendor record ***** 
                        VendorResponse = VendorValidateAndSave(Vendor);
                        if (VendorResponse.status == "Error") { break; }
                    }
                }
            }
            catch (SqlException ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunSQLErrorPackage = new RaygunExceptionPackage();
                myRaygunSQLErrorPackage.Tags.Add("SQL");
                myRaygunSQLErrorPackage.Tags.Add("Vendor");
                myRaygunClient.SendInBackground(ex, myRaygunSQLErrorPackage.Tags);

                VendorResponse.status = "SQL Exception Error";
                VendorResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + ex.Message.ToString());
            }
            catch (Exception ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunGenericErrorPackage = new RaygunExceptionPackage();
                myRaygunGenericErrorPackage.Tags.Add("Generic");
                myRaygunGenericErrorPackage.Tags.Add("Vendor");
                myRaygunClient.SendInBackground(ex, myRaygunGenericErrorPackage.Tags);

                VendorResponse.status = "Error";
                VendorResponse.errorList.Add("Generic error - " + Environment.NewLine + ex.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(VendorResponse);

            return myResponse;
        }


        //***** Validates Vendor record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
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
                VendorResponse.status = "Success";
            }
            catch (DataPortalException ex)
            {
                //TODO: ADH 10/12/2020 - TEST: All new Raygun exception handling works below
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunDataPortalPackage = new RaygunExceptionPackage();
                myRaygunDataPortalPackage.Tags.Add("Data Portal");
                myRaygunDataPortalPackage.Tags.Add("Vendor");
                myRaygunDataPortalPackage.CustomData.Add("WebApps Vendor ID", parmVendor.AccountingID);
                myRaygunClient.SendInBackground(ex, myRaygunDataPortalPackage.Tags, myRaygunDataPortalPackage.CustomData);

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
                        //***** Create Raygun Exception Package *****
                        RaygunExceptionPackage myRaygunValidationPackage = new RaygunExceptionPackage();
                        myRaygunValidationPackage.Tags.Add("Validation");
                        myRaygunValidationPackage.Tags.Add("Vendor");
                        myRaygunValidationPackage.CustomData.Add("WebApps Vendor ID", parmVendor.AccountingID);
                        Exception myValidationEx = new Exception("Validation error: " + rule.RuleName + " - " + rule.Description);
                        myRaygunClient.SendInBackground(myValidationEx, myRaygunValidationPackage.Tags, myRaygunValidationPackage.CustomData);

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
