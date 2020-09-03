using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;


namespace QuantifyWebAPI.Classes
{
    public class SQLHelper
    {
        private string strDbConn = "";

        //public SQLHelper()
        //{
        //    strDbConn = ConfigurationManager.ConnectionStrings["AsurioPortal"].ToString();
        //}

        //public SQLHelper(string StrDbConn)
        //{
        //    strDbConn = StrDbConn;
        //}

        public SqlConnection GetConnection(string StrDbConn)
        {
            SqlConnection c = new SqlConnection(StrDbConn);
            return c;
        }

        public DataTable FillTable(SqlCommand Cmd, String StrDbConn)
        {
            String strDbConn = "";

            if (StrDbConn.Length > 0)
            {
                strDbConn = StrDbConn;
            }
            else
            {
                strDbConn = this.strDbConn;
            }

            SqlDataAdapter adpt = new SqlDataAdapter(Cmd);
            adpt.SelectCommand.Connection = GetConnection(strDbConn);
            DataTable dt = new DataTable();

            Cmd.CommandTimeout = 180;

            using (Cmd.Connection)
            {
                Cmd.Connection.Open();
                adpt.Fill(dt);
            }

            return dt;
        }

        public DataSet FillDataSet(SqlCommand Cmd, String StrDbConn)
        {
            String strDbConn = "";

            if (StrDbConn.Length > 0)
            {
                strDbConn = StrDbConn;
            }
            else
            {
                strDbConn = this.strDbConn;
            }

            SqlDataAdapter adpt = new SqlDataAdapter(Cmd);
            adpt.SelectCommand.Connection = GetConnection(strDbConn);
            DataSet ds = new DataSet();

            Cmd.CommandTimeout = 180;

            using (Cmd.Connection)
            {
                Cmd.Connection.Open();
                adpt.Fill(ds);
            }

            return ds;
        }

        public String FillString(SqlCommand Cmd, String StrDbConn)
        {
            String strDbConn = "";

            if (StrDbConn.Length > 0)
            {
                strDbConn = StrDbConn;
            }
            else
            {
                strDbConn = this.strDbConn;
            }

            SqlDataAdapter adpt = new SqlDataAdapter(Cmd);
            adpt.SelectCommand.Connection = GetConnection(strDbConn);
            String str;

            Cmd.CommandTimeout = 180;

            using (Cmd.Connection)
            {
                Cmd.Connection.Open();
                str = (string)Cmd.ExecuteScalar();
            }

            return str;
        }

    }
}