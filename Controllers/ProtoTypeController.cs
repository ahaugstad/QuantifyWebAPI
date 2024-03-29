﻿// System References
using System;
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
using System.Configuration;

// Other References
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Controllers
{
    public class ProtoTypeController : ApiController
    {
        //***** https://localhost:44387/api/DataRouterPost/PingInitialization *****

        String StrVersionDBConn = ConfigurationManager.AppSettings["QuantifyPersistanceLayerDBConn"];
        RaygunClient myRaygunClient = new RaygunClient();
        String StrQuantifyUser = ConfigurationManager.AppSettings["QuantifyCredentials"];
        QuantifyCredentials myQuantifyCredentials;

        public ProtoTypeController()
        {
            string[] MyQuantCredArray = StrQuantifyUser.Split('|');
            myQuantifyCredentials = new QuantifyCredentials(MyQuantCredArray[0], MyQuantCredArray[1]);
        }

        #region Fiddler Prototypes 
        // GET: api/ProtoType
        public IEnumerable<string> GetArrayTest()
        {


            try
            {
                throw new System.ArgumentException("Parameter cannot be null", "original");

            }
            catch (Exception ex)
            {
                myRaygunClient.SendInBackground(ex);
            }

            return new string[] { "value1", "value2" };
        }

        // GET: api/ProtoType/5
        public string GetTest()
        {
            //***** Log on to Quantify *****
            QuantifyHelper QuantHelper = new QuantifyHelper(myQuantifyCredentials);


            QuantHelper.QuantifyLogin();
            return "value";
        }

        public string PlayingAround(int id)
        {
            //***** Invoices - have create, modify, invoice dates, no version stamp *****
            Guid invoiceID = new Guid();
            Invoice myInvoice = Invoice.GetInvoice(invoiceID, true);
            string modifyDate = myInvoice.ModifyDate;
            string createDate = myInvoice.CreateDate;
            string invoiceDate = myInvoice.InvoiceDate;
            InvoiceCollection myInvoiceCollection = InvoiceCollection.GetInvoiceCollection(InvoiceExportStatus.All);

            //***** Material transactions (Shipment, ?) - have date of transaction and version stamp, no modify date
            //*** Movement ***
            Guid purchaseID = new Guid();
            MovementGetAction getAction = new MovementGetAction();
            Movement myMovement = Movement.GetMovement(purchaseID, getAction);
            MovementCollection myMovementCollection = MovementCollection.GetMovementCollection(MovementType.All);
            string movementDate = myMovement.MovementDate;
            byte[] movementVersionStamp = myMovement.VersionStamp;
            //*** Shipment ***
            string shipmentID = "test";
            Shipment myShipment = Shipment.GetShipment(shipmentID, true, true);
            string shipCreateDate = myShipment.CreateDate;
            string shipmentDate = myShipment.ActualShipDate;
            byte[] shipmentVersionStamp = myShipment.VersionStamp;

            //***** Purchase Orders - do not have versioning as of now *****
            Guid orderID = new Guid();
            Order myOrder = Order.GetOrder(orderID);
            OrderCollection myOrderCollection = OrderCollection.GetPurchaseOrderCollection(Guid.Empty, ActiveStatus.Active);
            string myPONumber = myOrder.PurchaseOrderNumber;

            return "value2";
        }


        [HttpPost]
        public HttpResponseMessage UpsertCustomerData(JObject jsonResult)
        {


            string RequestType = jsonResult["entity"].ToString();




            //PackageDataType junkTest = Junkin.ToObject<PackageDataType>();
            // Initialization  
            HttpResponseMessage response = null;
            string myResponse = "";
            try
            {
                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass = jsonResult.ToObject<CustomerRootClass>();


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

        #region UpsertTemplateData
        [HttpPost]
        public HttpResponseMessage UpsertTemplateData(JObject jsonResult)
        {
            //***** Initialization *****
            HttpResponseMessage HttpResponse = null;
            string myResponse = "";

            //***** Instantiate response class for logging successes/errors if fail ***** 
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            //***** Try to Deserialize to Class object and Process Data *****
            try
            {
                //***** Log on to Quantify *****
                QuantifyHelper QuantHelper = new QuantifyHelper(myQuantifyCredentials);

                QuantHelper.QuantifyLogin();

                //***** Deserialize JObject to create class we can work with ******
                CustomerRootClass myDeserializedClass = jsonResult.ToObject<CustomerRootClass>();


                //***** Define fields *****
                //string CustomerNumber = myDeserializedClass.CustomerData.customer_id;
                //string CustomerName = myDeserializedClass.CustomerData.customer_name;
                //string CustomerPhone = myDeserializedClass.CustomerData.customer_phone;
                //string CustomerEmail = myDeserializedClass.CustomerData.customer_email;
                //string CustomerFax = myDeserializedClass.CustomerData.customer_fax;


                ////*****  Instantiate customer we are inserting/updating; check if it already exists before updating/inserting ***** 
                //BusinessPartner customer = BusinessPartner.GetBusinessPartnerByNumber(CustomerNumber);

                ////****** check to See if Create or Update *****
                //if (customer.PartnerNumber == "")
                //{
                //    //***** Create new customer ***** 
                //    customer = BusinessPartner.NewBusinessPartner(PartnerTypes.Customer);
                //    // BUSINESS DECISION: 
                //    // Confirm the following line is what we want to do, since there is auto-numbering conventions in Quantify. 
                //    // We always have the AccountingID field to sync a record up with WebApps if need be.
                //    customer.PartnerNumber = CustomerNumber;
                //}
                //else
                //{
                //    //***** Get existing customer ***** 
                //    customer = BusinessPartner.GetBusinessPartnerByNumber(CustomerNumber);
                //}

                ////***** Set non-address customer fields ***** 
                //customer.AccountingID = CustomerNumber;
                //customer.Name = CustomerName;
                //customer.PhoneNumber = CustomerPhone;
                //customer.EmailAddress = CustomerEmail;
                //customer.FaxNumber = CustomerFax;

                //***** Validate and save the Customer record ***** 
                //    customerResponse = customerValidateAndSave(customer);

                //    //***** Verify we have successfully saved Customer record's non-address fields before moving on to addresses - if not, skip and return errors
                //    if (customerResponse.status != "Error")
                //    {
                //        //***** Update appropriate address information for Customer based on address type provided ***** 
                //        foreach (QuantifyWebAPI.Classes.Address myAddress in myDeserializedClass.CustomerData.Address)
                //        {
                //            //***** Get state object for updating State ID below *****
                //            State state = State.GetState(myAddress.state);

                //            //***** Re-fetch customer record each time we update address data ***** 
                //            customer = BusinessPartner.GetBusinessPartnerByNumber(myDeserializedClass.CustomerData.customer_id);
                //            if (myAddress.addressTypeCode == "Business" || myAddress.addressTypeCode == null || myAddress.addressTypeCode == "")
                //            {
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).Street = myAddress.address1;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).Street1 = myAddress.address2;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).City = myAddress.city;
                //                // BUSINESS DECISION: should we be adding in non-existent states through code? Or should we throw an error and have users configure it in Quantify UI? 
                //                // Check if State exists - if it doesn't, first need to add it, before passing GUID into State ID *****
                //                //if (state.StateID == Guid.Empty)
                //                //{
                //                //    State newState = State.NewState();
                //                //    newState.Name = myAddress.state;
                //                //    newState.Save();
                //                //    customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = newState.StateID;
                //                //}
                //                //else
                //                //{
                //                //    customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                //                //}
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).StateName = myAddress.state;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).PostalCode = myAddress.zip;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).Country = myAddress.country;
                //            }
                //            else if (myAddress.addressTypeCode == "Billing")
                //            {
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).Street = myAddress.address1;
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).Street1 = myAddress.address2;
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).City = myAddress.city;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).StateName = myAddress.state;
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).PostalCode = myAddress.zip;
                //                customer.Addresses.GetAddressByType(AddressTypes.Billing).Country = myAddress.country;
                //            }
                //            else if (myAddress.addressTypeCode == "Shipping")
                //            {
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street = myAddress.address1;
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).Street1 = myAddress.address2;
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).City = myAddress.city;
                //                customer.Addresses.GetAddressByType(AddressTypes.Business).StateID = state.StateID;
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).StateName = myAddress.state;
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).PostalCode = myAddress.zip;
                //                customer.Addresses.GetAddressByType(AddressTypes.Shipping).Country = myAddress.country;
                //            }

                //            //***** Validate and save the Customer record ***** 
                //            customerResponse = customerValidateAndSave(customer);
                //            if (customerResponse.status != "Error") { customerResponse.status = "Success"; } else { break; }
                //        }
                //    }
            }
            catch (SqlException e)
            {
                customerResponse.status = "SQL Exception Error";
                customerResponse.errorList.Add("Quantify Login error - " + Environment.NewLine + e.Message.ToString());
            }
            catch (Exception e)
            {
                customerResponse.status = "Error";
                customerResponse.errorList.Add("Generic error - " + Environment.NewLine + e.Message.ToString());
            }

            //***** Serialize response class to Json to be passed back *****
            myResponse = JsonConvert.SerializeObject(customerResponse);

            HttpResponse = Request.CreateResponse(HttpStatusCode.OK);
            HttpResponse.Content = new StringContent(myResponse);

            return HttpResponse;
        }
        #endregion

        #region TemplateValidateAndSave
        //***** Validates customer record. If it validates, it saves and commits the changes. It if has errors, logs those to be passed back to Boomi. ***** 
        public CustomerResponseObj templateValidateAndSave(BusinessPartner parmCustomer)
        {
            //***** Create response object ***** 
            CustomerResponseObj customerResponse = new CustomerResponseObj();

            //***** Create string list for errors ***** 
            List<string> errorList = new List<string>();

            //if (parmCustomer.IsSavable)
            //{
            //    try
            //    {
            //        //***** Attempt to save ***** 
            //        parmCustomer.Save();
            //    }
            //    catch (DataPortalException ex)
            //    {
            //        //***** Pass back "Error" for fail ***** 
            //        customerResponse.status = "Error";

            //        //***** Get the object back from the data tier ***** 
            //        parmCustomer = ex.BusinessObject as BusinessPartner;

            //        //***** We can check to see if the name is unique ***** 
            //        if (!parmCustomer.IsUnique)
            //        {
            //            //***** Fix the name ***** 
            //        }
            //        else
            //        {
            //            //***** Check the rules ***** 
            //            foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
            //            {
            //                //***** Fix rules here, if you'd like ***** 
            //                //if (rule.Property == "Name")
            //                //{
            //                //***** Fix the name here  ***** 
            //                //}

            //                //***** Log errors and pass back response  ***** 
            //                errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    //***** Pass back "Error" for fail ***** 
            //    customerResponse.status = "Error";

            //    foreach (Avontus.Core.Validation.BrokenRule rule in parmCustomer.BrokenRulesCollection)
            //    {
            //        //***** Fix rules here, if you'd like ***** 
            //        //if (rule.Property == "Name")
            //        //{
            //        //***** Fix the name here  ***** 
            //        //}

            //        //***** Log errors and pass back response  ***** 
            //        errorList.Add(rule.Severity.ToString() + ": " + rule.Description);
            //    }

            //    customerResponse.errorList = errorList;
            //}
            return customerResponse;
        }
        #endregion

        #region APIFieldResearch

        public void Test()
        {
            MovementList all_Movements = MovementList.GetMovementList(Guid.Empty, MovementType.TransferNewToRent, true, Guid.Empty);

            foreach (MovementListItem movementItem in all_Movements)
            {
                //***** Movements (VersionStamp on both Collection and List) *****
                Movement myMovement = Movement.GetMovement(Guid.Empty, MovementGetAction.None);
                var moveVersionStamp = myMovement.VersionStamp;

                MovementList myMovementList = MovementList.GetMovementList(Guid.Empty, MovementType.All, false, Guid.Empty);
                foreach (MovementListItem movementListItem in myMovementList)
                {
                    var moveVersionStampList = movementListItem.VersionStamp;
                }

                //***** Products (VersionStamp on List but not Collection) *****
                Product myProduct = Product.GetProduct(Guid.Empty);
                //var prodVersionStamp = myProduct.VersionStamp;

                ProductList myProductList = ProductList.GetProductList(ProductType.All);
                foreach (ProductListItem productListItem in myProductList)
                {
                    var prodVersionStampList = productListItem.VersionStamp;
                }

                //***** Shipments *****
                Shipment myShipment = Shipment.GetShipment("", true, true);
                var shipVersionStamp = myShipment.VersionStamp;

                //***** Invoices *****
                InvoiceCollection invoices = InvoiceCollection.GetInvoiceCollection(InvoiceExportStatus.All);
                Invoice myInvoice = Invoice.GetInvoice(Guid.Empty,true);
                
                var invoiceVersionStamp = myInvoice.ModifyDate;

                //***** Orders (VersionStamp on List but not Collection) *****
                Order myOrder = Order.GetOrder(Guid.Empty);
                //var orderVersionStamp = myOrder.VersionStamp;

                OrderList myOrderList = OrderList.GetOrderList(Guid.Empty, OrderTypeEnum.Both, ActiveStatus.Both);
                foreach (OrderListItem orderListItem in myOrderList)
                {
                    var ordVersionStampList = orderListItem.VersionStamp;
                }

                //***** Jobsites (VersionStamp on Collection but not List) *****
                StockingLocation myJobSite = StockingLocation.GetStockingLocation("");
                var jobsiteVersionStamp = myJobSite.VersionStamp;

                StockingLocationList myJobSiteList = StockingLocationList.GetJobsites(false, JobTreeNodeDisplayType.Name, Guid.Empty);
                //foreach (StockingLocationListItem jobsiteListItem in myJobSiteList)
                //{
                //var jobVersionStampList = jobsiteListItem.VersionStamp;
                //}

                //***** Adding in Sales Tax Rates *****
                TaxRate myTaxRate = TaxRate.NewTaxRate();
                myTaxRate.Name = "Test";
                myTaxRate.Rate = 5;
                myTaxRate.RefID = "1234";
                myTaxRate.Description = "Longer Description";
                myTaxRate.TaxAgency = "South Dakota";

                TaxRate myTaxRate2 = TaxRate.GetTaxRate(myJobSite.JobTax1ID);

                TaxRateCollection myTaxRateCollection = TaxRateCollection.GetTaxRateCollection(ActiveStatus.Active, false, Guid.Empty);
                foreach (TaxRate myTaxRate3 in myTaxRateCollection)
                {
                    if (myTaxRate3.RefID == myTaxRate.RefID)
                    {
                        
                    }
                }
            }
        }
        #endregion
    }
}
