﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    /// <summary>
    /// Example: https://github.com/JoshClose/CsvHelper/blob/master/src/CsvHelper.Example/Program.cs
    /// Set with data loaded from a text file.
    /// 1. User chooses a file and some other params like header, encoding etc. (can be recommended). 
    /// 2. The system opens the file (connection) automatically analyzes its schema and sample data by recommending (default) import info (mapping, types, conversions etc.)
    ///    - Load column info (from header column or default values). Column count, column names etc.
    ///    - Load sample values into columns for all further analysis (size is defined by a parameter SampleSize)
    ///    - Suggesting data types for columns and converters
    ///    - Output of this procedure is filling initial values into the import spec object (like import dim) which will be shown to the user
    /// 3. User edits the import parameters (column names, column types, imported columns, column converters etc.) and presses Ok.
    ///    - Here we use simply an editor for changing import spec
    ///    - Possibly the system could suggest something also at this stage using sample values
    /// 4. Import spec is saved in import dimension (possibly extended class) and really loads the data (populate table).
    ///    - Use all parameters for creating import dimension
    ///    - Create import dim and add it to the schema
    ///    - Call standard table population method which will load the data, iterate through records, create output tuple from input tuple, add output tuple to the populated set
    /// </summary>
    public class SchemaCsv : Schema
    {
        #region Connection methods

        public ConnectionCsv connection; // Connection object for access to the native engine functions

        // Use name of the connection for setting schema name

        #endregion

        #region Schema methods

        public List<DcColumn> LoadSchema(SetCsv table) // Table object is created to store all necessary parameters which are individual for each table
        {
            List<DcColumn> columns = new List<DcColumn>();

            if (table.FilePath == null || !File.Exists(table.FilePath)) // File might not have been created (e.g., if it is an export file)
            {
                return columns;
            }
            
            connection.OpenReader(table);

            List<string> names = connection.ReadColumns();
            List<string[]> sampleRows = connection.ReadSampleValues();
            for (int i = 0; i < names.Count; i++)
            {
                string columnName = names[i];
                DcTable type = this.GetPrimitive("String");
                DimCsv column = (DimCsv)this.CreateColumn(columnName, table, type, false);
                column.ColumnIndex = i;
                columns.Add(column);
                //column.Add();

                var values = new List<string>();
                foreach (var row in sampleRows)
                {
                    values.Add(row[i]);
                }

                column.SampleValues = values;
            }

            //AddTable(table, null, null);

            connection.CloseReader();

            return columns;
        }

        #endregion

        #region ComSchema interface

        public override DcTable CreateTable(String name)
        {
            DcTable table = new SetCsv(name);
            return table;
        }

        public override DcTable AddTable(DcTable table, DcTable parent, string superName)
        {
            if (parent == null)
            {
                parent = Root;
            }
            if (string.IsNullOrEmpty(superName))
            {
                superName = "Super";
            }

            Dim dim = new DimCsv(superName, table, parent, true, true);

            dim.Add();

            return table;
        }

        public override DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            DcColumn dim = new DimCsv(name, input, output, isKey, false);

            return dim;
        }

        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Schema

        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Schema

        }

        #endregion

        protected override void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
        {
            Set set;
            Dim dim;

            set = new Set("Root");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            // Text files have only one type - String.
            set = new Set("String");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();
        }

        public SchemaCsv()
            : this("")
        {
        }

        public SchemaCsv(string name)
            : base(name)
        {
            DataSourceType = DataSourceType.CSV;
            connection = new ConnectionCsv();
        }

    }

}