using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.OleDb;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

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

        public void Open()
        {
            if (String.IsNullOrEmpty(ConnectionString))
            {
                return; 
            }

            if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
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

        public void ImportSchema()
        {
            List<string> tableNames = ReadTables();

            // Create all sets
            foreach (string tableName in tableNames)
            {
                Set set = new Set(tableName); // Create a set 
//                set.FromSetName = tableName;
                set.SuperDim = new DimRoot("super", set, this); // Add the new set to the schema by setting its super dimension
            }

            // Load columns and FKs as (complesx) paths and (simple) FK-dimensions
            foreach (string tableName in tableNames)
            {
                ImportPaths(tableName);
            }

            List<Set> sets = GetAllSubsets();
            foreach (Set set in sets)
            {
                foreach (Dim path in set.GreaterPaths.ToArray())
                {
                    path.ExpandPath();
                }
            }
        }

        /// <summary>
        /// The method loads schema data from the specified table in the underlying database and stores them in a matching set.
        /// The method loops through all columns and for each of them creates or finds existing three elements in the schema:
        /// a complex path from this set to the primtiive domain, a simple FK dimension from this set to the target FK set, and 
        /// a complex path from the target FK set to the primitive domain. The complex path corresponding to the column will 
        /// contain only two segments and this path definition has to be corrected later. 
        /// </summary>
        /// <param name="tableName"></param>
        private void ImportPaths(string tableName)
        {
            // Find set corresonding to this table
            Set tableSet = Root.FindSubset(tableName);

            Debug.Assert(!tableSet.IsPrimitive, "Wrong use: cannot load paths into a primitive set.");

            DataTable pks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, new object[] { null, null, tableName });
            DataTable fks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, new object[] { null, null, null, null, null, tableName });

            // Read all columns info
            DataTable columns = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tableName, null });
            foreach (DataRow col in columns.Rows)
            {
                string columnName = col["COLUMN_NAME"].ToString();
                string columnType = MapToLocalType(((OleDbType)col["DATA_TYPE"]).ToString());

                Dim path = tableSet.GetGreaterPath(columnName); // It might have been already added
                if (path == null)
                {
                    path = new Dim(columnName);
                    path.LesserSet = tableSet; // Assign domain set give the table name
                    path.GreaterSet = Root.GetPrimitiveSubset(columnType);
                    tableSet.AddGreaterPath(path); // We do not know if it is FK or simple dimensin so it needs to be checked and fixed later
                }

                // Find PKs this attribute belongs to (among all PK columns of this table)
                foreach (DataRow pk in pks.Rows)
                {
                    string pkColumnName = (string)pk["COLUMN_NAME"];
                    if (columnName.Equals(pkColumnName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        path.IsIdentity = true; // PK name pk["PK_NAME"] is not stored. It has to be stored then Dimension class has to be extended
                        break; // Assume that a column can belong to only one PK 
                    }
                }

                // Find FKs this attribute belongs to (among all FK columns of this table)
                foreach (DataRow fk in fks.Rows)
                {
                    if (!columnName.Equals((string)fk["FK_COLUMN_NAME"], StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Target PK name fk["PK_NAME"] is not stored and is not used because we assume that there is only one PK

                    //
                    // Step 1. Add FK-segment
                    //

                    string fkName = (string)fk["FK_NAME"];
                    Debug.Assert(tableSet.GetGreaterDim(fkName) == null, "A dimension already exists.");

                    Dim fkSegment = new Dim(fkName); // It is the first segment in the path representing this FK
                    fkSegment.LesserSet = tableSet;

                    string fkTargetTableName = (string)fk["PK_TABLE_NAME"]; // Name of the target set of the simple dimension (first segment of this complex path)
                    Set fkTargetSet = Root.FindSubset(fkTargetTableName);
                    fkSegment.GreaterSet = fkTargetSet;
                    tableSet.AddGreaterDim(fkSegment); // Add this FK-dimension to the set (it is always new)

                    if (path.Path.Count > 0)
                    {
                        path.Path[0] = fkSegment;
                    }
                    else
                    {
                        path.Path.Add(fkSegment);
                    }

                    //
                    // Step 2. Add rest of the path
                    //

                    string fkTargetColumnName = (string)fk["PK_COLUMN_NAME"]; // Next path name belonging to the target set
                    Dim fkTargetPath = fkTargetSet.GetGreaterPath(fkTargetColumnName); // This column might have been created
                    if (fkTargetPath == null)
                    {
                        fkTargetPath = new Dim(fkTargetColumnName);
                        fkTargetPath.LesserSet = fkTargetSet;
                        fkTargetPath.GreaterSet = path.GreaterSet;
                        fkTargetSet.AddGreaterPath(fkTargetPath); // We do not know if it is really a FK or simple dimension so this needs to be fixed later
                    }

                    if (path.Path.Count > 1)
                    {
                        path.Path[1] = fkTargetPath;
                    }
                    else
                    {
                        path.Path.Add(fkTargetPath);
                    }
                    break; // We assume that a column can belong to only one FK
                }

                if (path.Path.Count == 0) // Not FK - just normal column
                {
                    Dim dim = new Dim(path.Name);
                    dim.LesserSet = path.LesserSet;
                    dim.GreaterSet = path.GreaterSet;
                    dim.IsIdentity = path.IsIdentity;
                    dim.LesserSet.AddGreaterDim(dim);

                    path.Path.Add(dim); // The path will consist of a single segment
                }

            }

        }

        /// <summary>
        /// Load data corresponding the specified set from the underlying database. 
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        public override DataTable Export(Set set) // Dimensions are empty - data is in the remote database
        {
            // Check if this set is our child

            // Generate query for our database engine by including at least all identity dimensions
            string select = ""; // Attribute names or their definitions stored in the dimensions
            List<Dim> attributes = set.GreaterPaths; // We need our custom aliases with target platform definitions as db-specific column names
            foreach (Dim dim in attributes)
            {
                select += "[" + dim.Name + "]" + ", ";
            }
            select = select.Substring(0, select.Length - 2);

            string from = "[" + set.Name + "]";
            string where = ""; // set.WhereExpression
            string orderby = ""; // set.OrderbyExpression

            // Send query to the remote database for execution
            string query = "SELECT " + select + " FROM " + from + " ";
            query += String.IsNullOrEmpty(where) ? "" : "WHERE " + where + " ";
            query += String.IsNullOrEmpty(orderby) ? "" : "ORDER BY " + orderby + " ";

            // Read and return the result set
            DataSet dataSet = new DataSet();
            try
            {
                Open();
                using (OleDbCommand cmd = new OleDbCommand(query, _connection))
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

        public SetRootOledb(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            // We need to bootstrap the database with primitive types corresponding to OleDb standard
        }

    }

}
