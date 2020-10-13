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
    public class TaxCodeBusinessLogic
    {
        RaygunClient myRaygunClient = new RaygunClient();
        QuantifyHelper QuantHelper;
        string initializationMode;
        public TaxCodeBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public string UpsertTaxCodeData(JObject jsonResult)
        {
            //***** Initialization *****
            string myResponse = "Success";

            //***** Instantiate response class for logging successes/errors if fail ***** 
            TaxCodeResponseObj TaxCodeResponse = new TaxCodeResponseObj();

            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Log on to Quantify *****
                QuantHelper.QuantifyLogin();

                //***** Deserialize JObject to create class we can work with ******
                TaxCodeRootClass myDeserializedClass = jsonResult.ToObject<TaxCodeRootClass>();


                //***** Define fields *****
                string TaxCode = myDeserializedClass.TaxCodeData.tax_code;
                string TaxCodeDescription = myDeserializedClass.TaxCodeData.tax_code_description;
                string TaxCodeState = myDeserializedClass.TaxCodeData.tax_code_state;
                string TaxCodeRate = myDeserializedClass.TaxCodeData.tax_code_rate;

                //***** Query list of all Tax Rates to loop through and compare when Tax Code record is incoming ***** 
                TaxRateCollection myTaxCodeCollection = TaxRateCollection.GetTaxRateCollection(ActiveStatus.Both, false, Guid.Empty);

                //***** Loop through full list of Tax Rates and update matching record if one exists *****
                TaxRate inputTaxCode = TaxRate.NewTaxRate();
                foreach (TaxRate myTaxCode in myTaxCodeCollection)
                {
                    if (myTaxCode.RefID == TaxCode)
                    {
                        //***** Update existing TaxCode ***** 
                        myTaxCode.Name = TaxCodeState + " - " + TaxCodeDescription;
                        myTaxCode.Rate = Convert.ToDouble(TaxCodeRate);
                        myTaxCode.RefID = TaxCode;

                        //***** Assign to TaxCode object for writing to database *****
                        inputTaxCode = myTaxCode;
                    }
                    else
                    {
                        continue;
                    }
                }

                //***** If no matching record was found, fill out required fields and save new record *****
                if (inputTaxCode.IsNew)
                {
                    inputTaxCode.Name = TaxCodeState + " - " + TaxCodeDescription;
                    inputTaxCode.Rate = Convert.ToDouble(TaxCodeRate);
                    inputTaxCode.RefID = TaxCode;
                }

                //***** Validate and save the TaxCode record ***** 
                TaxCodeResponse = TaxCodeValidateAndSave(inputTaxCode);
            }
            catch (SqlException ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunSQLErrorPackage = new RaygunExceptionPackage();
                myRaygunSQLErrorPackage.Tags.Add("Generic");
                myRaygunSQLErrorPackage.Tags.Add("TaxCode");
                myRaygunClient.SendInBackground(ex, myRaygunSQLErrorPackage.Tags);

                TaxCodeResponse.status = "SQL Exception Error";
                TaxCodeResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + ex.Message.ToString());
            }
            catch (Exception ex)
            {
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunGenericErrorPackage = new RaygunExceptionPackage();
                myRaygunGenericErrorPackage.Tags.Add("Generic");
                myRaygunGenericErrorPackage.Tags.Add("TaxCode");
                myRaygunClient.SendInBackground(ex, myRaygunGenericErrorPackage.Tags);

                TaxCodeResponse.status = "Error";
                TaxCodeResponse.errorList.Add("Generic error - " + Environment.NewLine + ex.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(TaxCodeResponse);

            return myResponse;
        }


        //***** Validates TaxCode record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
        public TaxCodeResponseObj TaxCodeValidateAndSave(TaxRate parmTaxCode)
        {
            //***** Create response object ***** 
            TaxCodeResponseObj TaxCodeResponse = new TaxCodeResponseObj();

            //***** Create string list for errors ***** 
            List<string> errorList = new List<string>();

            try
            {
                //***** Attempt to save ***** 
                parmTaxCode.Save();
            }
            catch (DataPortalException ex)
            {
                //TODO: ADH 10/12/2020 - TEST: All new Raygun exception handling works below
                //***** Create Raygun Exception Package *****
                RaygunExceptionPackage myRaygunDataPortalPackage = new RaygunExceptionPackage();
                myRaygunDataPortalPackage.Tags.Add("Data Portal");
                myRaygunDataPortalPackage.Tags.Add("TaxCode");
                myRaygunDataPortalPackage.CustomData.Add("WebApps Tax Code", parmTaxCode.RefID);
                myRaygunClient.SendInBackground(ex, myRaygunDataPortalPackage.Tags, myRaygunDataPortalPackage.CustomData);

                //***** Pass back "Error" for fail ***** 
                TaxCodeResponse.status = "Error";

                //***** Get the object back from the data tier ***** 
                parmTaxCode = ex.BusinessObject as TaxRate;

                //***** We can check to see if the name is unique ***** 
                if (!parmTaxCode.IsUnique)
                {
                    //***** Fix the name ***** 
                }
                else
                {
                    //***** Check the rules ***** 
                    foreach (Avontus.Core.Validation.BrokenRule rule in parmTaxCode.BrokenRulesCollection)
                    {
                        //***** Create Raygun Exception Package *****
                        RaygunExceptionPackage myRaygunValidationPackage = new RaygunExceptionPackage();
                        myRaygunValidationPackage.Tags.Add("Validation");
                        myRaygunValidationPackage.Tags.Add("TaxCode");
                        myRaygunValidationPackage.CustomData.Add("WebApps Tax Code", parmTaxCode.RefID);
                        Exception myValidationEx = new Exception("Validation error: " + rule.RuleName + " - " + rule.Description);
                        myRaygunClient.SendInBackground(myValidationEx, myRaygunValidationPackage.Tags, myRaygunValidationPackage.CustomData);

                        //***** Log errors and pass back response  ***** 
                        errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
                    }
                    TaxCodeResponse.errorList = errorList;
                }
            }
            return TaxCodeResponse;
        }
    }
}
