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
        SQLHelper MySqlHelper = new SQLHelper();

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

            //***** Get all products and all consumables - need to do this in separate calls
            //***** Will loop through these collections and compare VersionStamp against appropriate record in our Products dictionary *****
            ProductCollection all_products = ProductCollection.GetProductCollection(ProductType.Product);
            ProductCollection all_consumables = ProductCollection.GetProductCollection(ProductType.Consumable);

            //***** Concatenate product and consumable collections together so we only need to loop once *****
            var combined_products = all_products.Concat(all_consumables);

            //***** Get DataTable Data Structure for Version Control Stored Procedure *****
            DataTable dt = MySqlHelper.GetVersionTableStructure();

            Dictionary<string, Product> myProductsDictionary = new Dictionary<string, Product>();

            //***** Loop through all products in both product and consumable catalogs *****
            foreach (Product product in combined_products)
            {
                string myProductID = product.PartNumber;
                
                //***** Add record to data table to be written to Version table in SQL *****
                MySqlHelper.CreateVersionDataRow(dt, "Product", myProductID, "");
      
                //TODO: ADH 9/14/2020 - Create dictionary build method
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

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            //DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            //if (myChangedRecords.Rows.Count > 0)
            //{
            ProductRootClass myProducts = new ProductRootClass();

            //***** Create Audit Log and XRef table structures *****            
            DataTable auditLog = MySqlHelper.GetAuditLogTableStructure();
            DataTable productXRef =  MySqlHelper.GetXRefTableStructure();

            //foreach (DataRow myRow in myChangedRecords.Rows)
            foreach (var myProductObj in myProductsDictionary)
            {
                //string myProductID = myRow["QuantifyID"].ToString();
                //Product myProduct = myProductsDictionary[myProductID];

                //***** Initialize classes to use in building data profile
                Product myProduct = (Product)myProductObj.Value;
                ProductData myProductData = new ProductData();
                ProductCategory myProductCategory = ProductCategory.GetProductCategory(myProduct.ProductCategoryID, myProduct.ProductType);

                //***** Build data profile *****
                myProductData.catalog = myProduct.ProductType.ToDescription();
                myProductData.category = myProductCategory.RevenueCode;
                myProductData.cost_code = myProductCategory.CostCode;
                myProductData.list_price = myProduct.DefaultList.ToString();
                myProductData.unit_cost = myProduct.DefaultCost.ToString();
                myProductData.product_id = myProductObj.Key;
                myProductData.description = myProduct.Description;

                //***** Package as class, serialize to JSON and write to data table to get mass inserted into SQL *****
                myProducts.entity = "Product";
                myProducts.Product = myProductData;
                string myJsonObject = JsonConvert.SerializeObject(myProducts);

                //***** Create audit log datarow ******                 
                auditLog = MySqlHelper.CreateAuditLogDataRow(auditLog, "Product", myProductData.product_id, myJsonObject, "", "A");

                //****** Create XRef datarow *****
                productXRef = MySqlHelper.CreateXRefDataRow(productXRef, myProductData.product_id, myProduct.PartNumber, "");
            }
            //***** Insert to Audit Log and XRef tables for Boomi to reference *****
            DataTable myReturnResultAudit = myDAL.InsertAuditLog(auditLog, connectionString);
            DataTable myReturnResultXRef = myDAL.InsertProductXRef(productXRef, connectionString);

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
