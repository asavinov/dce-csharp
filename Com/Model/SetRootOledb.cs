using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.OleDb;
using System.Data;

namespace Com.Model
{
    /// <summary>
    /// Set with data loaded using OleDb API.
    /// Data is loaded from any format using the corresponding OleDb provider. 
    /// A provider exposes a specific format using standard OleDb API.
    /// OleDb tutorial: http://msdn.microsoft.com/en-us/library/aa288452(v=vs.71).aspx
    /// Read schema: http://support.microsoft.com/kb/309681
    /// Connection string example: "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Test\\Northwind.accdb"
    /// Provider example: "Provider=Microsoft.Jet.OLEDB.4.0;"
    /// </summary>
    public class SetRootOledb : SetRoot
    {
        /// <summary>
        /// Connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        private OleDbConnection _connection;

        public override Set GetPrimitiveSet(Attribute attribute)
        {
            string typeName = null;

            // Type mapping
            switch (attribute.DataType)
            {
                case "Double": // System.Data.OleDb.OleDbType.Double
                    typeName = "Double";
                    break;
                case "Inteer": // System.Data.OleDb.OleDbType.Integer
                    typeName = "Integer";
                    break;
                case "Char": // System.Data.OleDb.OleDbType.Char
                case "VarChar": // System.Data.OleDb.OleDbType.VarChar
                case "VarWChar": // System.Data.OleDb.OleDbType.VarWChar
                case "WChar": // System.Data.OleDb.OleDbType.WChar
                    typeName = "String";
                    break;
                default:
                    typeName = "String"; // All the rest of types or error
                    break;
            }

            Set set = GetPrimitiveSet(typeName); // Find primitive set with this type
            return set;
        }

        public void Open()
        {
            if (String.IsNullOrEmpty(ConnectionString))
            {
                return; 
            }

            try
            {
                _connection = new OleDbConnection(ConnectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to create a database connection. \n{0}", ex.Message);
            }

            try
            {
                _connection.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to open the DataBase.\n{0}", ex.Message);
            }
        }

        public void Close()
        {
            if (_connection == null)
            {
                return;
            }

            try
            {
                _connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to close the DataBase.\n{0}", ex.Message);
            }
            finally
            {
                _connection = null;
            }
        }

        private List<string> ReadTables()
        {
            // Assumption: connection is open
            List<string> tableNames = new List<string>();

            // Read a table with schema information
            // For oledb: http://www.c-sharpcorner.com/UploadFile/Suprotim/OledbSchema09032005054630AM/OledbSchema.aspx
            DataTable tables = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });

            // Read table info
            foreach (DataRow row in tables.Rows)
            {
                string tableName = row["TABLE_NAME"].ToString();
                tableNames.Add(tableName);
            }

            return tableNames;
        }

        private List<Attribute> ReadAttributes(string tableName)
        {
            // Assumption: connection is open
            List<Attribute> attributes = new List<Attribute>();

            DataTable pks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, new object[] { null, null, tableName });
            DataTable fks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, new object[] { null, null, null, null, null, tableName });

            // Read all columns info
            DataTable columns = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tableName, null });
            foreach (DataRow col in columns.Rows)
            {
                string columnName = col["COLUMN_NAME"].ToString();
                Attribute attribute = new Attribute(columnName);
                attribute.TableName = tableName;
                attribute.TypeSystem = "OleDb"; // TODO: we need to standartize all type systems (providers?)
                OleDbType columnType = (OleDbType)col["DATA_TYPE"];
                attribute.DataType = columnType.ToString();

                // Find PKs this attribute belongs to
                foreach (DataRow pk in pks.Rows)
                {
                    if (columnName.Equals((string)pk["COLUMN_NAME"], StringComparison.InvariantCultureIgnoreCase))
                    {
                        attribute.PkName = (string)pk["PK_NAME"];
                        break; // Assume that a column can belong to only one PK
                    }
                }

                // Find FKs this attribute belongs to
                foreach (DataRow fk in fks.Rows)
                {
                    if (columnName.Equals((string)fk["FK_COLUMN_NAME"], StringComparison.InvariantCultureIgnoreCase))
                    {
                        attribute.FkName = (string)fk["FK_NAME"];
                        attribute.FkTargetTableName = (string)fk["PK_TABLE_NAME"];
                        attribute.FkTargetColumnName = (string)fk["PK_COLUMN_NAME"];
                        attribute.FkTargetPkName = (string)fk["PK_NAME"];
                        break; // Assume that a column can belong to only one FK
                    }
                }

                attributes.Add(attribute);
            }

            return attributes;
        }

        public void LoadSchema()
        {
            List<string> tableNames = ReadTables();
            foreach(string tableName in tableNames) 
            {
                Set set = new Set(tableName); // Create a set 
                set.SuperDim = new DimRoot("super", set, this); // Add the new set to the schema by setting its super dimension

                List<Attribute> attributes = ReadAttributes(tableName);
                set.Attributes = attributes;
            }

            List<Set> sets = GetAllSubsets();
            foreach (Set set in sets)
            {
                set.UpdateDimensions(); // Create dimensions from attributes
            }
        }

        public override DataTable Export(Set set) // Dimensions are empty - data is in the remote database
        {
            // Check if this set is our child

            // Generate query for our database engine by including at least all identity dimensions
            string select = ""; // Attribute names or their definitions stored in the dimensions
            List<Dimension> attributes = set.GetGreaterLeafDimensions(); // These must be primitive dimensions storing column names
            foreach (Dimension dim in attributes)
            {
                select += dim.Name + ", ";
            }
            select = select.Substring(0, select.Length - 2);

            string from = set.Name;
            string where = ""; // set.WhereExpression
            string orderby = ""; // set.OrderbyExpression

            // Send query to the remote database for execution
            string query = "SELECT " + select + " FROM " + from + " ";
            query += String.IsNullOrEmpty(where) ? "" : "WHERE " + where + " ";
            query += String.IsNullOrEmpty(orderby) ? "" : "ORDER BY " + orderby + " ";

            // Read and return the result set
            DataSet myDataSet = new DataSet();
            try
            {

                OleDbCommand myAccessCommand = new OleDbCommand(query, _connection);
                OleDbDataAdapter myDataAdapter = new OleDbDataAdapter(myAccessCommand);

                Open();

                myDataAdapter.Fill(myDataSet);

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

            Console.WriteLine("{0} tables in data set", myDataSet.Tables.Count);
            Console.WriteLine("{0} rows and {1} columns in table {2}", myDataSet.Tables[0].Rows.Count, myDataSet.Tables[0].Columns.Count, myDataSet.Tables[0].TableName);
            DataTable dataTable = myDataSet.Tables[0]; // We expect only one table

            return dataTable;
        }

        public SetRootOledb(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
        }
    }

}
