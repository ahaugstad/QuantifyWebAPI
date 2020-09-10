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
    public class ProductBusinessLogic
    {
        RaygunClient myRaygunClient = new RaygunClient();

        // GET: api/Jobs/3
        public string Initialize()
        {
            //TODO: ADH 9/9/2020 - Implement Initial Load of Quantities
            return "value";
        }

        public bool GetIDsToProcess(string connectionString)
        {
            bool success = true;

            QuantifyHelper QuantHelper = new QuantifyHelper();
            BoomiHelper BoomiHelper = new BoomiHelper();

            QuantHelper.QuantifyLogin();

            //***** Get all products - will loop through these collections and compare VersionStamp against appropriate record in our Products dictionary *****
            //TODO: ADH 9/9/2020 - Verify that this is what we want for ProductType (ProductOrConsumable)
            ProductCollection all_products = ProductCollection.GetProductCollection(ProductType.Product);
            ProductCollection all_consumables = ProductCollection.GetProductCollection(ProductType.Consumable);
            //ProductHistory productHistory = ProductHistory.GetProductHistory(Guid.Empty);
            //ProductConsumableList all_consumables = ProductConsumableList.GetProductConsumableList(Guid.Empty);
            //ProductComponentList all_components = ProductComponentList.GetProductComponentList(Guid.Empty);
            //StockedProductList all_stocked_products = StockedProductList.GetStockedProductList(Guid.Empty, ProductType.Product);
            DataTable dt = new DataTable();
            dt.Columns.Add("Entity", typeof(string));
            dt.Columns.Add("QuantifyID", typeof(string));
            dt.Columns.Add("Version", typeof(string));

            Dictionary<string, Product> myProductsDictionary = new Dictionary<string, Product>();

            //***** Loop through all products in product catalog *****
            foreach (Product product in all_products)
            {
                string myPartNumber = product.PartNumber;
                //***** If Product is Serialized, will need to create one record in Version table per Serialized Part *****
                if (product.IsSerialized)
                {
                    SerializedPartList serializedParts = SerializedPartList.GetSerializedPartList(product.ProductID, StockedStatus.Both);
                    foreach (SerializedPartListItem serializedPart in serializedParts)
                    {
                        string myProductID = myPartNumber + "|" + serializedPart.SerialNumber;

                        //***** Add record to data table to be written to Version table in SQL *****
                        DataRow myNewRow = dt.NewRow();
                        myNewRow["Entity"] = "Product";
                        myNewRow["QuantifyID"] = myProductID;
                        //string timestampVersion = "0x" + String.Join("", product.VersionStamp.Select(b => Convert.ToString(b, 16)));
                        //myNewRow["Version"] = timestampVersion.ToString();

                        dt.Rows.Add(myNewRow);

                        //***** Build Dictionary *****
                        try
                        {
                            if (!myProductsDictionary.ContainsKey(myProductID))
                            {
                                myProductsDictionary.Add(myProductID, product);
                            }
                            else
                            {
                                throw new System.ArgumentException("Duplicate product id", "Product Lookup");
                            }
                        }
                        catch (Exception ex)
                        {
                            myRaygunClient.SendInBackground(ex);
                        }
                    } 
                }
                else
                {
                    string myProductID = myPartNumber;

                    //***** Add record to data table to be written to Version table in SQL *****
                    DataRow myNewRow = dt.NewRow();
                    myNewRow["Entity"] = "Product";
                    myNewRow["QuantifyID"] = myProductID;
                    //string timestampVersion = "0x" + String.Join("", product.VersionStamp.Select(b => Convert.ToString(b, 16)));
                    //myNewRow["Version"] = timestampVersion.ToString();

                    dt.Rows.Add(myNewRow);

                    //***** Build Dictionary *****
                    try
                    {
                        if (!myProductsDictionary.ContainsKey(myProductID))
                        {
                            myProductsDictionary.Add(myProductID, product);
                        }
                        else
                        {
                            throw new System.ArgumentException("Duplicate product id", "Product Lookup");
                        }
                    }
                    catch (Exception ex)
                    {
                        myRaygunClient.SendInBackground(ex);
                    }
                }   
            }

            //***** Loop through all products in consumable catalog *****
            foreach (Product product in all_consumables)
            {
                string myPartNumber = product.PartNumber;
                //***** If Product is Serialized, will need to create one record in Version table per Serialized Part *****
                if (product.IsSerialized)
                {
                    SerializedPartList serializedParts = SerializedPartList.GetSerializedPartList(product.ProductID, StockedStatus.Both);
                    foreach (SerializedPartListItem serializedPart in serializedParts)
                    {
                        string myProductID = myPartNumber + "|" + serializedPart.SerialNumber;
                        //***** Add record to data table to be written to Version table in SQL *****
                        DataRow myNewRow = dt.NewRow();
                        myNewRow["Entity"] = "Product";
                        myNewRow["QuantifyID"] = myProductID;
                        //string timestampVersion = "0x" + String.Join("", product.VersionStamp.Select(b => Convert.ToString(b, 16)));
                        //myNewRow["Version"] = timestampVersion.ToString();

                        dt.Rows.Add(myNewRow);

                        //***** Build Dictionary *****
                        try
                        {
                            if (!myProductsDictionary.ContainsKey(myProductID))
                            {
                                myProductsDictionary.Add(myProductID, product);
                            }
                            else
                            {
                                throw new System.ArgumentException("Duplicate product id", "Product Lookup");
                            }
                        }
                        catch (Exception ex)
                        {
                            myRaygunClient.SendInBackground(ex);
                        }
                    }
                }
                else
                {
                    string myProductID = myPartNumber;
                    //***** Add record to data table to be written to Version table in SQL *****
                    DataRow myNewRow = dt.NewRow();
                    myNewRow["Entity"] = "Product";
                    myNewRow["QuantifyID"] = myProductID;
                    //string timestampVersion = "0x" + String.Join("", product.VersionStamp.Select(b => Convert.ToString(b, 16)));
                    //myNewRow["Version"] = timestampVersion.ToString();

                    dt.Rows.Add(myNewRow);

                    //***** Build Dictionary *****
                    try
                    {
                        if (!myProductsDictionary.ContainsKey(myProductID))
                        {
                            myProductsDictionary.Add(myProductID, product);
                        }
                        else
                        {
                            throw new System.ArgumentException("Duplicate product id", "Product Lookup");
                        }
                    }
                    catch (Exception ex)
                    {
                        myRaygunClient.SendInBackground(ex);
                    }
                } 
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            //DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            //if (myChangedRecords.Rows.Count > 0)
            //{
            ProductRootClass myProducts = new ProductRootClass();

            //***** Create Audit Log and XRef table structures *****
            DataTable auditLog = new DataTable();
            auditLog.Columns.Add("QuantifyID", typeof(string));
            auditLog.Columns.Add("Entity", typeof(string));
            auditLog.Columns.Add("PackageSchema", typeof(string));
            auditLog.Columns.Add("QuantifyDepartment", typeof(string));
            auditLog.Columns.Add("ProcessStatus", typeof(string));

            DataTable productXRef = new DataTable();
            productXRef.Columns.Add("QuantifyID", typeof(string));
            productXRef.Columns.Add("PartNumber", typeof(string));

            //foreach (DataRow myRow in myChangedRecords.Rows)
            foreach (var myProductObj in myProductsDictionary)
            {
                //string myProductID = myRow["QuantifyID"].ToString();
                //Product myProduct = myProductsDictionary[myProductID];
                Product myProduct = (Product) myProductObj.Value;  
                    
                //***** Populate Fields *****
                ProductData myProductData = new ProductData();

                //TODO: ADH 9/10/2020 - See if workaround for getting cost code without doing a lookup in loop for Product Category
                ProductCategory myProductCategory = ProductCategory.GetProductCategory(myProduct.ProductCategoryID, myProduct.ProductType);

                myProductData.catalog = myProduct.ProductType.ToDescription();
                myProductData.category = myProduct.ProductCategoryName;
                myProductData.cost_code = myProductCategory.CostCode;
                myProductData.list_price = myProduct.DefaultList.ToString();
                myProductData.unit_cost = myProduct.DefaultCost.ToString();

                if (myProduct.IsSerialized)
                {
                    char[] separator = { '|' };
                    string[] productIDSplit = myProductObj.Key.Split(separator);
                    string serialNumber = productIDSplit[productIDSplit.Length - 1];
                    SerializedPart serializedPart = SerializedPart.GetSerializedPart(serialNumber);
                    myProductData.product_id = myProductObj.Key;
                    myProductData.description = serializedPart.Description;

                    //***** Package as class, serialize to JSON and write to data tables to get mass inserted into SQL *****
                    myProducts.entity = "Product";
                    myProducts.Product = myProductData;
                    string myJsonObject = JsonConvert.SerializeObject(myProducts);

                    DataRow myNewRow = auditLog.NewRow();
                    myNewRow["QuantifyID"] = myProductData.product_id;
                    myNewRow["Entity"] = "Product";
                    myNewRow["PackageSchema"] = myJsonObject;
                    myNewRow["QuantifyDepartment"] = "";
                    myNewRow["ProcessStatus"] = "A";

                    auditLog.Rows.Add(myNewRow);

                    DataRow myNewXRefRow = productXRef.NewRow();
                    myNewXRefRow["QuantifyID"] = myProductData.product_id;
                }
                else
                {
                    myProductData.product_id = myProductObj.Key;
                    myProductData.description = myProduct.Description;

                    //***** Package as class, serialize to JSON and write to data table to get mass inserted into SQL *****
                    myProducts.entity = "Product";
                    myProducts.Product = myProductData;
                    string myJsonObject = JsonConvert.SerializeObject(myProducts);

                    DataRow myNewRow = auditLog.NewRow();

                    myNewRow["QuantifyID"] = myProductData.product_id;
                    myNewRow["Entity"] = "Product";
                    myNewRow["PackageSchema"] = myJsonObject;
                    myNewRow["QuantifyDepartment"] = "";
                    myNewRow["ProcessStatus"] = "A";

                    auditLog.Rows.Add(myNewRow);
                }     
            }
            //***** Insert to Audit Log and XRef tables for Boomi to reference *****
            DataTable myReturnResultAudit = myDAL.InsertAuditLog(auditLog, connectionString);
            DataTable myReturnResultXRef = myDAL.InsertAuditLog(auditLog, connectionString);

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

            BoomiHelper.PostBoomiAPI();
            //}
            return success;
        }
    }
}
