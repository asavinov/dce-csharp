using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.OleDb;
using System.Data;
using System.Diagnostics;

using Com.Utils;

using Rowid = System.Int32;

namespace Com.Schema.Rel
{
    /// <summary>
    /// Data is loaded from any format using the corresponding OleDb provider. 
    /// A provider exposes a specific format using standard OleDb API.
    /// OleDb tutorial: http://msdn.microsoft.com/en-us/library/aa288452(v=vs.71).aspx
    /// Read schema: http://support.microsoft.com/kb/309681
    /// Connection string example: "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Test\\Northwind.accdb"
    /// Provider example: "Provider=Microsoft.Jet.OLEDB.4.0;"
    /// </summary>
    public class ConnectionOledb
    {
        /// <summary>
        /// Connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        private OleDbConnection conn;

        public void Open()
        {
            if (String.IsNullOrEmpty(ConnectionString))
            {
                return;
            }

            if (conn != null && conn.State == System.Data.ConnectionState.Open)
            {
                return;
            }

            try
            {
                conn = new OleDbConnection(ConnectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to create a database connection. \n{0}", ex.Message);
            }

            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to open the DataBase.\n{0}", ex.Message);
            }
        }

        public void Close()
        {
            if (conn == null)
            {
                return;
            }

            try
            {
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to close the DataBase.\n{0}", ex.Message);
            }
            finally
            {
                conn = null;
            }
        }

        public string GetName()
        {
            if (!string.IsNullOrEmpty(conn.Database))
                return conn.Database;
            else if (!string.IsNullOrEmpty(conn.DataSource))
                return System.IO.Path.GetFileNameWithoutExtension(conn.DataSource);
            else
                return null;
        }

        public DataTable GetPks(string tableName)
        {
            DataTable pks = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, new object[] { null, null, tableName });
            return pks;
        }

        public DataTable GetFks(string tableName)
        {
            DataTable fks = null;
            try
            {
                fks = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, new object[] { null, null, null, null, null, tableName });
            }
            catch (Exception e) // For csv, foreign keys are not supported exception is raised
            {
                fks = new DataTable();
            }
            return fks;
        }

        public DataTable GetColumns(string tableName)
        {
            DataTable cols = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tableName, null });
            return cols;
        }

        public List<string> ReadTables()
        {
            // Assumption: connection is open
            List<string> tableNames = new List<string>();

            // Read a table with schema information
            // For oledb: http://www.c-sharpcorner.com/UploadFile/Suprotim/OledbSchema09032005054630AM/OledbSchema.aspx
            DataTable tables = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });

            // Read table info
            foreach (DataRow row in tables.Rows)
            {
                string tableName = row["TABLE_NAME"].ToString();
                tableNames.Add(tableName);
            }

            return tableNames;
        }

        public DataTable ExecuteSelect(string query)
        {
            // Read and return the result set
            DataSet dataSet = new DataSet();
            try
            {
                Open();
                using (OleDbCommand cmd = new OleDbCommand(query, this.conn))
                {
                    using (OleDbDataAdapter adapter = new OleDbDataAdapter(cmd))
                    {
                        adapter.Fill(dataSet);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to retrieve the required data from the DataBase.\n{0}", ex.Message);
                return null;
            }
            finally
            {
                Close();
            }

            Console.WriteLine("{0} tables in data set", dataSet.Tables.Count);
            Console.WriteLine("{0} rows and {1} columns in table {2}", dataSet.Tables[0].Rows.Count, dataSet.Tables[0].Columns.Count, dataSet.Tables[0].TableName);
            DataTable dataTable = dataSet.Tables[0]; // We expect only one table

            return dataTable;
        }

    }

}
