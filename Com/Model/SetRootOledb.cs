﻿using System;
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

        /// <summary>
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

            DataTable pks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, new object[] { null, null, tableName });
            DataTable fks = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, new object[] { null, null, null, null, null, tableName });

            // Read all columns info
            DataTable columns = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tableName, null });
            foreach (DataRow col in columns.Rows)
            {
                string columnName = col["COLUMN_NAME"].ToString();
                string columnType = MapToLocalType(((OleDbType)col["DATA_TYPE"]).ToString());

                Dimension path = tableSet.GetGreaterPath(columnName); // It might have been already added
                if (path == null)
                {
                    path = new Dimension(columnName);
                    path.LesserSet = tableSet; // Assign domain set give the table name
                    path.GreaterSet = Root.GetPrimitiveSet(columnType);
                    tableSet.AddGreaterPath(path); // We do not know if it is FK or simple dimensin so it needs to be checked and fixed later
                }

                // Find PKs this attribute belongs to (among all PK columns of this table)
                foreach (DataRow pk in pks.Rows)
                {
                    string pkColumnName = (string)pk["COLUMN_NAME"];
                    if (columnName.Equals(pkColumnName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        path.Identity = true; // PK name pk["PK_NAME"] is not stored. It has to be stored then Dimension class has to be extended
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
                    Dimension fkSegment = tableSet.GetGreaterDimension(fkName);
                    if (fkSegment != null)
                    {
                        // ERROR: Wrong use.
                    }

                    fkSegment = new Dimension(fkName); // It is the first segment in the path representing this FK
                    fkSegment.LesserSet = tableSet;

                    string fkTargetTableName = (string)fk["PK_TABLE_NAME"]; // Name of the target set of the simple dimension (first segment of this complex path)
                    Set fkTargetSet = Root.FindSubset(fkTargetTableName);
                    fkSegment.GreaterSet = fkTargetSet;
                    tableSet.AddGreaterDimension(fkSegment); // Add this FK-dimension to the set (it is always new)

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
                    Dimension fkTargetPath = fkTargetSet.GetGreaterPath(fkTargetColumnName); // This column might have been created
                    if (fkTargetPath == null)
                    {
                        fkTargetPath = new Dimension(fkTargetColumnName);
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

            }

        }

        public string MapToLocalType(string externalType)
        {
            string localType = null;

            switch (externalType)
            {
                case "Double": // System.Data.OleDb.OleDbType.Double
                    localType = "Double";
                    break;
                case "Inteer": // System.Data.OleDb.OleDbType.Integer
                    localType = "Integer";
                    break;
                case "Char": // System.Data.OleDb.OleDbType.Char
                case "VarChar": // System.Data.OleDb.OleDbType.VarChar
                case "VarWChar": // System.Data.OleDb.OleDbType.VarWChar
                case "WChar": // System.Data.OleDb.OleDbType.WChar
                    localType = "String";
                    break;
                default:
                    localType = "String"; // All the rest of types or error
                    break;
            }

            return localType;
        }

        public void ImportSchema()
        {
            List<string> tableNames = ReadTables();

            // Create all sets
            foreach(string tableName in tableNames) 
            {
                Set set = new Set(tableName); // Create a set 
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
                foreach (Dimension path in set.GreaterPaths.ToArray())
                {
                    path.ExpandPath();
                }
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
            DataSet dataSet = new DataSet();
            try
            {
                Open();
                using (OleDbCommand cmd = new OleDbCommand())
                {
                    cmd.CommandText = query;
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
        }

        #region Deprecated
/*
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
*/
/*
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
*/

        #endregion
    }

}
