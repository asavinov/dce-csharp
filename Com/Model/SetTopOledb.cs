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
    /// </summary>
    public class SetTopOledb : SetTop
    {
        #region Connection methods

        public ConnectionOledb connection; // Connection object for access to the native engine functions

        // Use name of the connection for setting schema name

        #endregion

        #region Schema methods

        public List<ComTable> LoadTables(List<string> tableNames = null)
        {
            List<ComTable> tables = new List<ComTable>();

            if (tableNames == null)
            {
                tableNames = connection.ReadTables();
            }

            //
            // Create all sets
            //
            foreach (string tableName in tableNames)
            {
                SetRel set = new SetRel(tableName); // Create a set 
                set.RelationalTableName = tableName;
                // Relational PK name will be set during column loading

                tables.Add(set);
            }

            //
            // Add them to the schema
            //
            foreach (ComTable table in tables)
            {
                AddTable(table, null, null);
            }

            return tables;
        }

        public void LoadSchema()
        {
            connection.Open();

            List<ComTable> tables = LoadTables();

            List<DimAttribute> attributes = new List<DimAttribute>();

            foreach (SetRel table in tables)
            {
                List<DimAttribute> atts = LoadAttributes(table);
                attributes.AddRange(atts);
            }

            connection.Close();

            List<ComColumn> columns = new List<ComColumn>();
            attributes.ForEach(a => a.ExpandAttribute(attributes, columns));

            // Add dims and paths to the sets
            attributes.ForEach(a => a.Add());
            columns.ForEach(c => c.Add());
        }

//
// What is Set::AddAllNonStoredPaths() ???
//

/*
        public void ImportSchema(List<string> tableNames = null)
        {
            if (tableNames == null)
            {
                tableNames = connection.ReadTables();
            }

            // Create all sets
            foreach (string tableName in tableNames)
            {
                Set set = new Set(tableName); // Create a set 
                set.RelationalTableName = tableName;
                this.AddTable(set, null);
            }

            // Load columns and FKs as (complex) paths and (simple) FK-dimensions
            foreach (string tableName in tableNames)
            {
                ImportPaths(tableName);
            }

            List<CsTable> sets = Root.GetAllSubsets();
            foreach (CsTable set in sets)
            {
                foreach (DimPath path in set.GreaterPaths)
                {
                    path.ExpandPath();
                }
            }

            foreach (Set set in sets)
            {
                set.AddAllNonStoredPaths();
            }
        }
*/

        protected List<DimAttribute> LoadAttributes(SetRel tableSet) 
        {
            // The created paths will be not be added to the schema (should be added manually)
            // The created paths are empty and do not store any dimensions (should be done/expanded separately by using the meta-data about PKs, FKs etc.)
            Debug.Assert(!tableSet.IsPrimitive, "Wrong use: cannot load structure for primitive set.");

            List<DimAttribute> attributes = new List<DimAttribute>();

            string tableName = tableSet.RelationalTableName;

            DataTable pks = connection.GetPks(tableName);
            DataTable fks = connection.GetFks(tableName);
            DataTable columns = connection.GetColumns(tableName);

            foreach (DataRow col in columns.Rows) // Process all columns of the table (correspond to primitive paths of the set)
            {
                string columnName = col["COLUMN_NAME"].ToString();
                string columnType = ((OleDbType)col["DATA_TYPE"]).ToString();
                ComTable typeTable = Top.GetPrimitive(columnType);

                //
                // Create an attribute object representing this column
                //
                DimAttribute path = tableSet.GetGreaterPathByColumnName(columnName); // It might have been already created (when processing other tables)
                if (path != null) continue;

                path = new DimAttribute(columnName, tableSet, typeTable);

                //
                // Set relational attribute of the object
                //

                path.RelationalColumnName = columnName;

                // Find PKs this attribute belongs to (among all PKs of this table)
                foreach (DataRow pk in pks.Rows)
                {
                    if (!StringSimilarity.SameColumnName(columnName, (string)pk["COLUMN_NAME"])) continue;

                    // Found PK this column belongs to
                    path.RelationalPkName = (string)pk["PK_NAME"];
                    tableSet.RelationalPkName = path.RelationalPkName; // OPTIMIZE: try to do it only once rather than for each attribute and try to identify and exclude multiple PKs (error)

                    //path.IsIdentity = true; // We simply have to override this property as "RelationalPkName != null" or "RelationalPkName == table.RelationalPkName"
                    break; // Assume that a column can belong to only one PK 
                }

                // Find FKs this attribute belongs to (among all FKs of this table)
                foreach (DataRow fk in fks.Rows)
                {
                    if (!StringSimilarity.SameColumnName(columnName,(string)fk["FK_COLUMN_NAME"])) continue;

                    // Target PK name fk["PK_NAME"] is not stored and is not used because we assume that there is only one PK

                    path.RelationalFkName = (string)fk["FK_NAME"];
                    path.RelationalTargetTableName = (string)fk["PK_TABLE_NAME"]; // Name of the target set of the simple dimension (first segment of this complex path)
                    path.RelationalTargetColumnName = (string)fk["PK_COLUMN_NAME"]; // Next path/attribute name belonging to the target set

                    break; // We assume that a column can belong to only one FK and do not continue with the rest of the FK-loop
                }

                attributes.Add(path);
            }

            return attributes;
        }

        /// <summary>
        /// The method loads structure of the specified table and stores it as a set in this schema.
        /// 
        /// The method loops through all columns and for each of them creates or finds existing three elements in the schema:
        /// a complex path from this set to the primtiive domain, a simple FK dimension from this set to the target FK set, and 
        /// a complex path from the target FK set to the primitive domain. The complex path corresponding to the column will 
        /// contain only two segments and this path definition has to be corrected later. 
        /// </summary>
        /// <param name="tableName"></param>
/*
        private void ImportPaths(string tableName)
        {
            // Find set corresonding to this table
            CsTable tableSet = Root.FindSubset(tableName);

            Debug.Assert(!tableSet.IsPrimitive, "Wrong use: cannot load paths into a primitive set.");

            DataTable pks = connection.conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, new object[] { null, null, tableName });
            DataTable fks = null;
            try
            {
                fks = connection.conn.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, new object[] { null, null, null, null, null, tableName });
            }
            catch (Exception e) // For csv, foreign keys are not supported exception is raised
            {
                fks = new DataTable();
            }

            DataTable columns = connection.conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tableName, null });
            foreach (DataRow col in columns.Rows) // Process all columns of the table (correspond to primitive paths of the set)
            {
                string columnName = col["COLUMN_NAME"].ToString();
                string columnType = ((OleDbType)col["DATA_TYPE"]).ToString();

                DimAttribute path = tableSet.GetGreaterPathByColumnName(columnName); // It might have been already created (when processing other tables)
                if (path == null)
                {
                    path = new DimAttribute(columnName);
                    path.RelationalColumnName = columnName;
                    path.LesserSet = tableSet; // Assign domain set give the table name
                    path.GreaterSet = Top.GetPrimitiveSubset(columnType);
                    tableSet.AddGreaterPath(path); // We do not know if it is FK or simple dimensin. It will be determined later.
                }

                // Find PKs this attribute belongs to (among all PKs of this table)
                foreach (DataRow pk in pks.Rows)
                {
                    string pkColumnName = (string)pk["COLUMN_NAME"];
                    if (!columnName.Equals(pkColumnName, StringComparison.InvariantCultureIgnoreCase)) continue;
                    string pkName = (string)pk["PK_NAME"];

                    // Found PK this column belongs to
                    path.RelationalPkName = pkName;
                    path.LesserSet.RelationalPkName = pkName; // OPTIMIZE: try to do it only once rather than for each attribute and try to identify and exclude multiple PKs (error)
                    path.IsIdentity = true;
                    break; // Assume that a column can belong to only one PK 
                }

                // Find FKs this attribute belongs to (among all FKs of this table)
                foreach (DataRow fk in fks.Rows)
                {
                    if (!columnName.Equals((string)fk["FK_COLUMN_NAME"], StringComparison.InvariantCultureIgnoreCase)) continue;

                    // Target PK name fk["PK_NAME"] is not stored and is not used because we assume that there is only one PK

                    //
                    // Step 1. Add FK-segment (as a dimension)
                    //

                    string fkName = (string)fk["FK_NAME"];
                    path.RelationalFkName = fkName;
                    Debug.Assert(tableSet.GetGreaterDim(fkName) == null, "A dimension already exists.");

                    Dim fkSegment = new Dim(fkName); // It is the first segment in the path representing this FK
                    fkSegment.RelationalFkName = fkName;
                    fkSegment.IsIdentity = path.IsIdentity;
                    fkSegment.RelationalPkName = path.RelationalPkName; // We assume if one column belongs t PK then the whole FK (this column belongs to) belongs to the same PK

                    fkSegment.LesserSet = tableSet;

                    string fkTargetTableName = (string)fk["PK_TABLE_NAME"]; // Name of the target set of the simple dimension (first segment of this complex path)
                    Set fkTargetSet = Root.FindSubset(fkTargetTableName);
                    fkSegment.GreaterSet = fkTargetSet;
                    fkSegment.Add(); // Add this FK-dimension to the set (it is always new)

                    if (path.Path.Count == 0)
                    {
                        path.Path.Add(fkSegment);
                    }
                    else
                    {
                        path.Path[0] = fkSegment; // Or we can insert it before all other segments
                        Debug.Assert(true, "Wrong use: A primary key dimension must be inserted only as the very first segment - not the second.");
                    }

                    //
                    // Step 2. Add rest of the path (as a path)
                    //

                    string fkTargetColumnName = (string)fk["PK_COLUMN_NAME"]; // Next path name belonging to the target set
                    DimPath fkTargetPath = fkTargetSet.GetGreaterPathByColumnName(fkTargetColumnName); // This column might have been created
                    if (fkTargetPath == null)
                    {
                        fkTargetPath = new DimPath(fkTargetColumnName);
                        fkTargetPath.RelationalColumnName = fkTargetColumnName;
                        fkTargetPath.IsIdentity = path.IsIdentity;
                        fkTargetPath.LesserSet = fkTargetSet;
                        fkTargetPath.GreaterSet = path.GreaterSet;
                        fkTargetSet.AddGreaterPath(fkTargetPath); // We do not know if it is really a FK or simple dimension so this needs to be fixed later
                    }

                    if (path.Path.Count == 0)
                    {
                        Debug.Assert(true, "Wrong use: A target path must be inserted only as the second segment - not any other.");
                    }
                    else if (path.Path.Count == 1)
                    {
                        path.Path.Add(fkTargetPath);
                    }
                    else
                    {
                        path.Path[1] = fkTargetPath;
                        Debug.Assert(true, "Wrong use: A target path must be inserted only as the second segment - not any other.");
                    }

                    break; // We assume that a column can belong to only one FK and do not continue with the rest of the FK-loop
                }

                if (path.Path.Count == 0) // Not FK - just normal column
                {
                    Dim dim = new Dim(path.Name);
                    dim.RelationalColumnName = path.RelationalColumnName;
                    dim.RelationalPkName = path.RelationalPkName;
                    dim.LesserSet = path.LesserSet;
                    dim.GreaterSet = path.GreaterSet;
                    dim.IsIdentity = path.IsIdentity;
                    dim.Add();

                    path.Path.Add(dim); // The path will consist of a single segment
                }

            }

        }
*/

        #endregion

        #region Data methods

        /// <summary>
        /// Load data corresponding to the specified set from the underlying database. 
        /// </summary>
        /// <returns></returns>
        public DataTable LoadTable(ComTable table) // Load data for only this table (without greater tables connected via FKs)
        {
            SetRel set = (SetRel)table;

            string select = "";
            List<DimAttribute> attributes = set.GreaterPaths;
            foreach (DimAttribute att in attributes)
            {
                select += "[" + att.RelationalColumnName + "]" + ", ";
            }
            select = select.Substring(0, select.Length - 2);

            string from = "[" + set.RelationalTableName + "]";
            string where = ""; // set.WhereExpression
            string orderby = ""; // set.OrderbyExpression

            // Send query to the remote database for execution
            string query = "SELECT " + select + " FROM " + from + " ";
            query += String.IsNullOrEmpty(where) ? "" : "WHERE " + where + " ";
            query += String.IsNullOrEmpty(orderby) ? "" : "ORDER BY " + orderby + " ";

            connection.Open();
            DataTable dataTable = connection.ExecuteSelect(query);
            connection.Close();

            return dataTable;
        }

/*
        public override DataTable LoadTableTree(Set set) // Load data for the specified and all greater tables connected via FKs
        {
            string query = BuildSql(set);

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

        public string BuildSql(Set set)
        {
            // Nested joins (Employee references Deparatment which references Company):
            // SELECT *
            // FROM Company
            //   LEFT JOIN (
            //     Department INNER JOIN Employee ON Department.ID = Employee.DepartmentID
            //   ) ON Company.ID = Department.CompanyID

            // Joining several detail tables (it is essentially sequential join):
            // SELECT [A].[Employee ID], [B].[Privilege Name], C.ID
            // FROM 
            // (( [Employee Privileges] AS [A] 
            // LEFT OUTER JOIN [Privileges] AS [B] ON [A].[Privilege ID] = [B].[Privilege ID] )
            // LEFT OUTER JOIN [Employees] AS [C] ON [A].[Employee ID] = [C].[ID] )

            //
            // Build SELECT
            // 
            string select = BuildSelect(set);

            //
            // Build FROM
            //
            string from = BuildFromSequential(new List<Dim>(), set); // From joins all greater tables
            int joinCount = from.Count(x => x == '(');
            from = from.Replace("(", "");
            from = new String('(', joinCount) + from;

            return "SELECT " + select + " FROM " + from;
        }
        private string BuildSelect(Set set)
        {
            // It is called one time and enumerates all attributes collected directly or indirectly from all greater tables using all primitive paths
            // Local attribute names from greater tables may not be unique and have to be renamed into this table paths

            // Since one table can be used more than once (like shipping Address and payment Address), we need unique table names.
            // Table names have to use unique aliases set in FROM for all greater tables
            // We do not select from intermediate tables which cosists of only FKs. 

            string res = "";
            foreach (DimPath att in set.GreaterPaths)
            {
                string tableAlias = GetTableAlias(att.LastSegment.LesserSet.RelationalTableName, GetPathHash(att.Path, att.Path.Count - 1));
                tableAlias = "[" + tableAlias + "]";

                string columnName = "[" + att.LastSegment.RelationalColumnName + "]"; // It is always a primitive segment with original primitive column name
                string coumnAlias = "[" + att.Name + "]"; // The same as path name
                res += tableAlias +"." + columnName + " AS " + coumnAlias + ", ";
            }
            res = res.Substring(0, res.Length - 2); // Remove suffix

            return res;
        }
        private string BuildFromSequential(List<Dim> lesserPath, Set set)
        {
            // This table and, sequentially. join all direct and indirect child tables
            // Each of these child tables is recursively represented as a sequential join of their own child tables
            // Lesser path is used to generate a unique table alias for this table

            string thisTable = "[" + set.RelationalTableName + "] " + "AS " + "[" + GetTableAlias(set.RelationalTableName, GetPathHash(lesserPath, lesserPath.Count)) + "] ";

            string res = "";
            if (lesserPath.Count == 0) // This table is not attached to the previous table - it is the first table in FROM
            {
                res += thisTable + " ";
            }
            else // This table is attached to the parent
            {
                res += "(LEFT OUTER JOIN " + thisTable + " ON ";

                Dim lesserDim = lesserPath[lesserPath.Count - 1];
                lesserPath.RemoveAt(lesserPath.Count - 1);
                res += BuildJoin(lesserPath, lesserDim.LesserSet, lesserDim) + ") ";
                lesserPath.Add(lesserDim);
            }

            // Now attach sequentially all child tables (and they will attach their child tables)
            string children = "";

            foreach (Dim dim in set.GreaterDims) // One join for each greater set
            {
                if (dim.IsPrimitive) continue; // Skip primitive dims, join only non-primitive sets
                if (!dim.GreaterSet.IsIn(set.Top)) continue; // Skip inter-schema dimensions (e.g., import dimensions)

                lesserPath.Add(dim);
                string childTable = BuildFromSequential(lesserPath, dim.GreaterSet);
                lesserPath.RemoveAt(lesserPath.Count - 1);

                children += childTable;
            }

            res += children;

            return res;
        }

        private string BuildFromNested(List<Dim> lesserPath, Set set)
        {
            // Sequentially join all direct child tables.
            // However, each of these child tables is recursively represented as a sequential join of their own child tables
            // Lesser path is used to generate a unique table alias for this table

            string res = "";

            string children = "";

            foreach (Dim dim in set.GreaterDims) // One join for each greater set
            {
                if (dim.IsPrimitive) continue; // Skip primitive dims, join only non-primitive sets

                res += "(";

                lesserPath.Add(dim);
                string childTable = BuildFromNested(lesserPath, dim.GreaterSet);
                lesserPath.RemoveAt(lesserPath.Count-1);

                children += "LEFT OUTER JOIN (" + childTable + ") ON ";

                children += BuildJoin(lesserPath, set, dim) + "";

                children += ") ";
            }

            // If there were no joined tables (no greater non-primitive sets) then return just one table with alias
            res += "[" + set.RelationalTableName + "] " + "AS " + "[" + GetTableAlias(set.RelationalTableName, GetPathHash(lesserPath, lesserPath.Count)) + "] ";
            res += children;

            return res;
        }

        private string BuildJoin(List<Dim> lesserPath, Set set, Dim gDim)
        {
            // Join criteria are specified using original table attributes which can be equal and therefore have to use table names as prefix.
            // Joins attributes: this table attribute vs. greater table attribute
            // We take (list) all primitive FK (dimension) attributes of this table vs. all primitive identity attributes of the greater table.
            // So we need a subroutine for generating a native join criterion for one greater set. The result is a string in native terms (using native attribute names). 
            // Which attributes (this or greater set) are used in this SELECT? The greater table can return attributes by joining its own greater tables.
            // Equality is specified using table names because attribute names are not unique: T.Id = gT.Id

            Set gSet = gDim.GreaterSet;
            List<DimPath> fkAtts = set.GetGreaterPathsStartingWith(new List<Dim>(new Dim[] { gDim })); // All columns belonging to this FK
            
            string res = "";
            foreach (DimPath att in fkAtts)
            {
                // Debug.Assert(att.RelationalPkName == gSet.RelationalPkName, "Wrong use: dimension/path relational attributes are not set correctly.");
                // Debug.Assert(att.RelationalFkName == gDim.RelationalFkName, "Wrong use: dimension/path relational attributes are not set correctly.");

                // Build tail by excluding the first segment (gDim)
                DimPath tail = new DimPath(gDim.Name);
                tail.Path = new List<Dim>(att.Path);
                tail.Path.RemoveAt(0);
                tail.LesserSet = tail.Path[0].LesserSet;

                // Find a path in the greater set which has this same tail
                DimPath gAtt = gSet.GetGreaterPath(tail);
                Debug.Assert(gAtt != null, "Wrong use: Tail path must exist.");

                if (!gAtt.IsIdentity) continue; // Join is made on only values (OPTIMIZE: we can retrieve only value-attributes before the loop rather than all possible paths, and then Assert here that it is identity path.)
                                                // GetGreaterPrimitiveDims(DimensionType.IDENTITY)

                // For each FK-column in this table create a join condition with the corresponding (tail) PK-column in the greater table
                string thisName = "[" + GetTableAlias(set.RelationalTableName, GetPathHash(lesserPath, lesserPath.Count)) + "].[" + att.RelationalColumnName + "]";
                
                lesserPath.Add(gDim);
                string gName = "[" + GetTableAlias(gSet.RelationalTableName, GetPathHash(lesserPath, lesserPath.Count)) + "].[" + gAtt.RelationalColumnName + "]";
                lesserPath.RemoveAt(lesserPath.Count-1);

                res += thisName + " = " + gName + " AND ";
            }

            res = res.Substring(0, res.Length - 4); // Remove suffix AND

            return res;
        }

        private static string GetPathHash(List<Dim> path, int count)
        {
            int pathToTableHash = 0;
            for (int i = 0; i < count; i++) // Compute hash of the path leading to this table
            {
                pathToTableHash += path[i].Id.GetHashCode();
            }

            pathToTableHash = Math.Abs(pathToTableHash);
            string hash = pathToTableHash.ToString("X"); // unique hash representing this path
            return hash.Length > 6 ? hash.Substring(0, 6) : hash;
        }

        private static string GetTableAlias(string tableName, string pathName)
        {
            return tableName + "_" + pathName;
        }

*/
        #endregion

        #region CsSchema interface

        public override ComTable CreateTable(String name)
        {
            ComTable table = new SetRel(name);
            return table;
        }

        public override ComColumn CreateColumn(string name, ComTable input, ComTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            ComColumn dim = new DimRel(name, input, output, isKey, false);

            return dim;
        }

        #endregion

        protected override void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
        {
            Set set;
            Dim dim;

            set = new Set("Root");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            // Either use Ole DB standard or System.Data.OleDb.OleDbType.* (or maybe they are the same). 
            // Type names must correspond to what we see in SQL queries (or other syntactic expressions expected by OleDb driver)
            foreach (OleDbType dataType in (OleDbType[])Enum.GetValues(typeof(OleDbType)))
            {
                set = new Set(dataType.ToString());
                dim = new Dim("Top", set, this, true, true);
                dim.Add();
            }
        }

        public SetTopOledb(string name)
            : base(name)
        {
            DataSourceType = DataSourceType.OLEDB;
        }

    }

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
