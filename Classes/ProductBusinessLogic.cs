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
    public class ProductBusinessLogic
    {
        //***** Initialize Raygun Client and Helper classes
        RaygunClient myRaygunClient = new RaygunClient();
        SQLHelper MySqlHelper = new SQLHelper();
        QuantifyHelper QuantHelper;
        string initializationMode;

        public ProductBusinessLogic(QuantifyCredentials QuantCreds, string InitializationMode)
        {
            QuantHelper = new QuantifyHelper(QuantCreds);
            initializationMode = InitializationMode;
        }

        // GET: api/Jobs/3
        public string Initialize()
        {
            return "value";
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantHelper.QuantifyLogin();

            //***** Get all products and all consumables - will loop through this list and compare VersionStamp against appropriate record in Versions table *****
            ProductList products_list = ProductList.GetProductList(ProductType.Product);
            ProductList consumables_list = ProductList.GetProductList(ProductType.Consumable);

            //***** Concatenate product and consumable collections together so we only need to loop once *****
            var combined_products = products_list.Concat(consumables_list);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, ProductListItem> myProductsDictionary = new Dictionary<string, ProductListItem>();

            Dictionary<string, string> myErrorDictionary = new Dictionary<string, string>();

            //***** Loop through all products in both product and consumable catalogs *****
            foreach (ProductListItem productListItem in combined_products)
            {
                string myProductID;
                //TODO: ADH 10/12/2020 - TEST: First 15 chars of string line below works for products
                if (productListItem.PartNumber.Length < 15)
                {
                    myProductID = productListItem.PartNumber.Substring(0, productListItem.PartNumber.Length);
                }
                else
                {
                    myProductID = productListItem.PartNumber.Substring(0, 15);
                }    
                string timestampVersion = "0x" + String.Join("", productListItem.VersionStamp.Select(b => Convert.ToString(b, 16)));
                
                //***** Add record to data table to be written to Version table in SQL *****
                dt = MySqlHelper.CreateVersionDataRow(dt, "Product", myProductID, timestampVersion);
      
                //***** Build Dictionary *****
                try
                {
                    if (!myProductsDictionary.ContainsKey(myProductID))
                    {
                        myProductsDictionary.Add(myProductID, productListItem);
                    }
                    else
                    {
                        myErrorDictionary.Add(myProductID,"Duplicate product id");
                    }
                }
                catch (Exception ex)
                {
                    //***** Create Raygun Exception Package *****
                    RaygunExceptionPackage myRaygunValidationPackage = new RaygunExceptionPackage();
                    myRaygunValidationPackage.Tags.Add("Validation");
                    myRaygunValidationPackage.Tags.Add("Product");
                    myRaygunValidationPackage.CustomData.Add("Product ID: ",myProductID);
                    
                    myRaygunClient.SendInBackground(ex, myRaygunValidationPackage.Tags, myRaygunValidationPackage.CustomData);
                }
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);

            //***** If in Initialization Mode bypass Data integrations other than Version Controll *****
            //if (initializationMode != "1")
            //{
                if (myChangedRecords.Rows.Count > 0)
                {
                    ProductRootClass myProducts = new ProductRootClass();

                    //***** Create Audit Log and XRef table structures *****            
                    DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();
                    DataTable productXRef = MySqlHelper.GetXRefTableStructure();

                    foreach (DataRow myRow in myChangedRecords.Rows)
                    {
                        //***** Initialize error tracking fields and data package *****
                        var myErrorText = "";
                        string myProcessStatus = "A";
                        string myProductID = myRow["QuantifyID"].ToString();
                        ProductData myProductData = new ProductData();

                        //***** Check if duplicate product ID and log error if so (in both Raygun and Audit Log table) *****
                        if (myErrorDictionary.ContainsKey(myProductID)) 
                        { 
                            myErrorText = myErrorDictionary[myProductID];
                            Exception myValidationException = new Exception("Duplicate Product ID: " + myProductID);
                            throw myValidationException;
                        }

                        //***** Initialize classes to use in building data profile *****
                        ProductListItem myProductListItem = myProductsDictionary[myProductID];
                        Product myProduct = Product.GetProduct(myProductListItem.ProductID);
                        ProductCategory myProductCategory = ProductCategory.GetProductCategory(myProduct.ProductCategoryID, myProduct.ProductType);

                        //***** Get WebApps Product ID from Products XRef and assign as Product ID if it exists *****
                        DataTable myProductXRefRecord = myDAL.GetWebAppsIDProductsXRef(myProduct.ProductID.ToString(), connectionString);
                        if (myProductXRefRecord.Rows.Count > 0)
                        {
                            string myWebAppsProductID = myProductXRefRecord.Rows[0]["WebAppsItemNumber"].ToString();
                            if (myWebAppsProductID != null && myWebAppsProductID != "")
                            {
                                myProductData.product_id = myWebAppsProductID;
                            }
                            else
                            {
                                myProductData.product_id = myProductID;
                            }
                        }
                        else
                        {
                            myProductData.product_id = myProductID;
                        }    

                        if (myProduct.ProductCategoryID != null && myProduct.ProductCategoryID != Guid.Empty)
                        {
                            //***** Build data profile *****
                            myProductData.catalog = myProduct.ProductType.ToDescription();
                            myProductData.category = myProductCategory.RevenueCode;
                            myProductData.list_price = myProduct.DefaultList.ToString();
                            myProductData.unit_cost = myProduct.DefaultCost.ToString();
                            myProductData.description = myProduct.Description;

                            //***** Package as class, serialize to JSON and write to data table to get mass inserted into SQL *****
                            myProducts.entity = "Product";
                            myProducts.Product = myProductData;
                            string myJsonObject = JsonConvert.SerializeObject(myProducts);

                            //***** Create audit log datarow *****                 
                            auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Product", myProductData.product_id, myJsonObject, "", myProcessStatus, myErrorText);
                            productXRef = MySqlHelper.UpsertXRefDataRow(productXRef, myProduct.ProductID.ToString(), myProductData.product_id, myProduct.PartNumber, "");
                        }
                    }
                    
                    //***** Insert to Audit Log and XRef tables for Boomi to reference *****
                    DataTable myReturnResultAudit = myDAL.InsertAuditLog(auditLog, connectionString);
                    DataTable myReturnResultXRef = myDAL.UpsertProductXRef(productXRef, connectionString);

                    string resultAudit = myReturnResultAudit.Rows[0][0].ToString();
                    string resultXRef = myReturnResultXRef.Rows[0][0].ToString();
                    if (resultAudit.ToLower() == "success" && resultXRef.ToLower() == "success")
                    {
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
            //}
            return success;
        }
    }
}
