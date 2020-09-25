using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;

using Mindscape.Raygun4Net;


namespace QuantifyWebAPI.Classes
{
    public class SQLHelper
    {
        RaygunClient myRaygunClient = new RaygunClient();

        private string strDbConn = "";


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

        #region VersionHelper
        public DataTable GetVersionTableStructure()
        {
            DataTable MyDataTable = new DataTable();
            MyDataTable.Columns.Add("Entity", typeof(string));
            MyDataTable.Columns.Add("QuantifyID", typeof(string));
            MyDataTable.Columns.Add("Version", typeof(string));

            return MyDataTable;
        }

        public DataTable CreateVersionDataRow(DataTable TargetDataTable, String Entity, String QuantifyID, String Version)
        {
            DataRow myNewRow = TargetDataTable.NewRow();
            myNewRow["Entity"] = Entity;
            myNewRow["QuantifyID"] = QuantifyID;     
            myNewRow["Version"] = Version;
            TargetDataTable.Rows.Add(myNewRow);

            return TargetDataTable;
        }
        #endregion

        #region AuditLogHelper
        public DataTable GetAuditLogTableStructure()
        {
            DataTable MyDataTable = new DataTable();
            MyDataTable.Columns.Add("QuantifyID", typeof(string));
            MyDataTable.Columns.Add("Entity", typeof(string));
            MyDataTable.Columns.Add("PackageSchema", typeof(string));
            MyDataTable.Columns.Add("QuantifyDepartment", typeof(string));
            MyDataTable.Columns.Add("ProcessStatus", typeof(string));
            MyDataTable.Columns.Add("ErrorMessage", typeof(string));

            return MyDataTable;
        }

        public DataTable CreateAuditLogDataRow(DataTable TargetDataTable, String Entity, String QuantifyID, String PackageSchema, String QuantifyDepartment, String ProcessStatus)
        {
            DataRow myNewRow = TargetDataTable.NewRow();
            myNewRow["Entity"] = Entity;
            myNewRow["QuantifyID"] = QuantifyID;
            myNewRow["PackageSchema"] = PackageSchema;
            myNewRow["QuantifyDepartment"] = QuantifyDepartment;
            myNewRow["ProcessStatus"] = ProcessStatus;
            //myNewRow["ErrorMessage"] = ErrorMessage;
            TargetDataTable.Rows.Add(myNewRow);

            return TargetDataTable;
        }
        #endregion

        #region XRefHelper
        public DataTable GetXRefTableStructure()
        {
            DataTable MyDataTable = new DataTable();
            MyDataTable.Columns.Add("QuantifyID", typeof(string));
            MyDataTable.Columns.Add("PartNumber", typeof(string));
            MyDataTable.Columns.Add("SerialNumber", typeof(string));


            return MyDataTable;
        }

        public DataTable CreateXRefDataRow(DataTable TargetDataTable, String QuantifyID, String PartNumber, String SerialNumber)
        {
            DataRow myNewRow = TargetDataTable.NewRow();            
            myNewRow["QuantifyID"] = QuantifyID;
            myNewRow["PartNumber"] = PartNumber;
            myNewRow["SerialNumber"] = SerialNumber;
            TargetDataTable.Rows.Add(myNewRow);

            return TargetDataTable;
        }
        #endregion
    }
}