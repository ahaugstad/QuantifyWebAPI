using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using Mindscape.Raygun4Net;

namespace QuantifyWebAPI.Classes
{
    public class DAL
    {
        private string constr;
        RaygunClient myRaygunClient = new RaygunClient();

        public DAL()
        {
            
        }

        public DataTable GetChangedObjects(DataTable EntityVersionTbl, String DbConnectionStr)
        {
            DataTable dt = new DataTable();

            try
            {

                using (SqlConnection conn = new SqlConnection(DbConnectionStr))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "GetChangedObjects";       

                        SqlParameter tvpParam = cmd.Parameters.AddWithValue("@EntityVersionTV", EntityVersionTbl);
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
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                //***** ReThrow Error to bubble it up to calling Classs ******
                throw new Exception(
                    string.Format("There was an error Getting Changed Objects."), ex);
            }

            return dt;

        }

        public DataTable InsertAuditLog(DataTable ObjectsAudit, String DbConnectionStr)
        {
            DataTable dt = new DataTable();

            try
            {

                using (SqlConnection conn = new SqlConnection(DbConnectionStr))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "InsertAuditLog";

                        SqlParameter tvpParam = cmd.Parameters.AddWithValue("@AuditLogTV", ObjectsAudit);
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
                //***** log the error ******
                myRaygunClient.SendInBackground(ex);

                //***** ReThrow Error to bubble it up to calling Classs ******

                throw new Exception(
                    string.Format("There was an error inserting to the audit log database."), ex);
            }

            return dt;

        }

    }
}