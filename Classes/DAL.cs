using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;

namespace QuantifyWebAPI.Classes
{
    public class DAL
    {
        private string constr;

        public DAL()
        {
            constr = "Server=tcp:apitesting.database.windows.net,1433;Initial Catalog=APi_Construction;Persist Security Info=False;User ID=API_Admin;Password=Winter20;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        }

        public DataTable InsertMaterialList(String EntityType, DataTable EntityVersionTbl)
        {
            DataTable dt = new DataTable();

            try
            {

                using (SqlConnection conn = new SqlConnection(constr))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "usp_GetChangedObjects";

                        cmd.Parameters.AddWithValue("EntityType", EntityType);

                        SqlParameter tvpParam = cmd.Parameters.AddWithValue("@MaterialDataTV", EntityVersionTbl);
                        tvpParam.SqlDbType = SqlDbType.Structured;

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {


                            da.Fill(dt);
                        }

                    }

                }
            }
            catch (Exception ex)

            {
                //log the error
                throw new Exception(
                    string.Format("There was an error Getting Changed Objects."), ex);
            }

            return dt;

        }
    }
}