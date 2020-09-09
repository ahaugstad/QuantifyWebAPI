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

            //***** Get all jobsites - will loop through this and compare VersionStamp against appropriate record in our JobVersions dictionary *****
            //TODO: ADH 9/9/2020 - Verify that this is what we want for ProductType (ProductOrConsumable)
            ProductCollection all_products = ProductCollection.GetProductCollection(ProductType.ProductOrConsumable);
            DataTable dt = new DataTable();
            dt.Columns.Add("Entity", typeof(string));
            dt.Columns.Add("QuantifyID", typeof(string));
            dt.Columns.Add("Version", typeof(string));

            Dictionary<string, Product> myProductsDictionary = new Dictionary<string, Product>();

            foreach (Product product in all_products)
            {
                string myPartNumber = product.PartNumber;
                DataRow myNewRow = dt.NewRow();
                myNewRow["Entity"] = "Product";
                myNewRow["QuantifyID"] = myPartNumber;
                //string timestampVersion = "0x" + String.Join("", product.VersionStamp.Select(b => Convert.ToString(b, 16)));
                //myNewRow["Version"] = timestampVersion.ToString();

                dt.Rows.Add(myNewRow);

                //***** Build Dictionary *****
                if(!myProductsDictionary.ContainsKey(myPartNumber))
                {
                    myProductsDictionary.Add(myPartNumber, product);
                } 
            }

            //***** Call data access layer *****
            DAL myDAL = new DAL();
            //DataTable myChangedRecords = myDAL.GetChangedObjects(dt, connectionString);


            //if (myChangedRecords.Rows.Count > 0)
            //{
                ProductRootClass myProducts = new ProductRootClass();

                //***** Create audit log table structure *****
                DataTable auditLog = new DataTable();
                auditLog.Columns.Add("QuantifyID", typeof(string));
                auditLog.Columns.Add("Entity", typeof(string));
                auditLog.Columns.Add("PackageSchema", typeof(string));
                auditLog.Columns.Add("QuantifyDepartment", typeof(string));
                auditLog.Columns.Add("ProcessStatus", typeof(string));

            //foreach (DataRow myRow in myChangedRecords.Rows)
            foreach (var myProductObj in myProductsDictionary)
                {
                    //string myProductID = myRow["QuantifyID"].ToString();
                    //Product myProduct = myProductsDictionary[myProductID];
                    Product myProduct = (Product) myProductObj.Value;  
                    
                    //***** Populate Fields *****
                    ProductData myProductData = new ProductData();

                    //TODO: ADH 9/9/2020 - Change this to go look at our XREF table for Products/Serial Numbers to determine the number to pass to Boomi
                    myProductData.product_id = myProduct.PartNumber;
                    myProductData.description = myProduct.Description;
                    myProductData.catalog = myProduct.ProductType.ToDescription();
                    myProductData.category = myProduct.ProductCategoryName;
                    //TODO: ADH 9/9/2020 - Verify if this is cost code and change accordingly
                    myProductData.cost_code = myProduct.UPCCode;
                    myProductData.list_price = myProduct.DefaultList.ToString();
                    myProductData.unit_cost = myProduct.DefaultCost.ToString();

                    //***** Package as class, serialize to JSON and write to audit log table *****
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
                //***** Create audit log record for Boomi to go pick up *****
                // REST API URL: http://apimariaasad01.apigroupinc.api:9090/ws/rest/webapps_quantify/api
                DataTable myReturnResult = myDAL.InsertAuditLog(auditLog, connectionString);

                //TODO: ADH 9/4/2020 - Figure out why following line is failing
                string result = myReturnResult.Rows[0][0].ToString();
                if (result.ToLower() == "success")
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
