// System References
using System;
using System.Data;
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
using System.Threading.Tasks;

// Quantify API References
using Avontus.Core;
using Avontus.Rental.Utility;
using Avontus.Rental.Library;
using Avontus.Rental.Library.Accounting;
using Avontus.Rental.Library.Accounting.XeroAccounting;
using Avontus.Rental.Library.Security;
using Avontus.Rental.Library.ToolWatchImport;
using Avontus.Rental.Library.Logging;

// Internal Class references
using QuantifyWebAPI.Classes;

// Other References
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;


namespace QuantifyWebAPI.Controllers
{
    public class JobPOBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        SharedHelper mySharedHelper = new SharedHelper();
        QuantifyHelper QuantHelper;
        string initializationMode;

        public JobPOBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all job POs - will loop through this and compare VersionStamp against appropriate record in Versions table *****
            MovementCollection all_jobPOs = MovementCollection.GetMovementCollection(MovementType.ReRentOrdered);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Movement> myJobPOsDictionary = new Dictionary<string, Movement>();

            foreach (Movement myJobPO in all_jobPOs)
            {
                {
                    string myJobPONumber = myJobPO.MovementNumber;
                    string timestampVersion = "0x" + String.Join("", myJobPO.VersionStamp.Select(b => Convert.ToString(b, 16)));

                    //***** Add record to data table to be written to Version table in SQL *****
                    dt = MySqlHelper.CreateVersionDataRow(dt, "JobPO", myJobPONumber, timestampVersion.ToString());

                    //***** Build Dictionary *****
                    if (!myJobPOsDictionary.ContainsKey(myJobPONumber))
                    {
                        myJobPOsDictionary.Add(myJobPONumber, myJobPO);
                    }
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);

            //***** If in Initialization Mode bypass Data integrations other than Version Controll *****
            if (initializationMode != "1")
            { 

                if (myChangedRecords.Rows.Count > 0)
                {
                    //***** Initialize activity log variables and data model class *****
                    DateTime myStartDate = DateTime.Now;
                    int processedRecordCount = 0;
                    JobPORootClass myJobPOs = new JobPORootClass();

                    //***** Create Audit Log and XRef table structures *****
                    DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        JobPOData myJobPOData = new JobPOData();

                        //***** Initalize fields and classes to be used to build data profile *****
                        string myJobPOID = myRow["QuantifyID"].ToString();
                        Movement myJobPO = myJobPOsDictionary[myJobPOID];
                        BusinessPartner myVendor = BusinessPartner.GetBusinessPartnerByNumber(myJobPO.MovementBusinessPartnerNumber);

                        //***** Build header data profile *****                   
                        myJobPOData.transaction_number = myJobPO.MovementNumber;
                        myJobPOData.vendor_number = myVendor.AccountingID;
                        myJobPOData.job_number = myJobPO.BackOrderNumber;
                        myJobPOData.total = myJobPO.MovementTotal.ToString();

                        //TODO: ADH 9/24/2020 - Still need to find where Entered By field is in Quantify, if anywhere
                        myJobPOData.entered_by = "QuantifyInt";

                        //***** Package as class, serialize to JSON and write to audit log table *****
                        myJobPOs.entity = "JobPO";
                        myJobPOs.JobPO = myJobPOData;
                        string myJsonObject = JsonConvert.SerializeObject(myJobPOs);

                        //***** Create audit log datarow ******                 
                        auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "JobPO", myJobPOData.transaction_number, myJsonObject, "", myProcessStatus, myErrorText);
                        processedRecordCount++;
                    }

                    //***** Create audit log record for Boomi to go pick up *****
                    DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

                    //***** Create activity log record for reference *****
                    DataTable myActivityLog = myDAL.InsertClassActivityLog("JobPOs", "", processedRecordCount, myStartDate, DateTime.Now, connectionString);

                    string result = myReturnResult.Rows[0][0].ToString();
                    if (result.ToLower() == "success")
                    {
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            return success;
        }
    }
}
