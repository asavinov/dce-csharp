using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Offset = System.Int32;

namespace Com.Model
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

        public List<ComColumn> LoadSchema(SetCsv table) // Table object is created to store all necessary parameters which are individual for each table
        {
            connection.Open(table);

            List<ComColumn> columns = new List<ComColumn>();
            List<string> names = connection.GetColumns();
            List<string[]> sampleRows = connection.GetSampleValues();
            for (int i = 0; i < names.Count; i++)
            {
                string columnName = names[i];
                ComTable type = this.GetPrimitive("String");
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

            connection.Close();

            return columns;
        }

        #endregion

        #region ComSchema interface

        public override ComTable CreateTable(String name)
        {
            ComTable table = new SetCsv(name);
            return table;
        }

        public override ComColumn CreateColumn(string name, ComTable input, ComTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            ComColumn dim = new DimCsv(name, input, output, isKey, false);

            return dim;
        }

        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Schema

        }

        public override void FromJson(JObject json, Workspace ws)
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

    /// <summary>
    /// Access to a (text) file. Connection is specified by file name.
    /// </summary>
    public class ConnectionCsv
    {
        // Various parameters for reading the file:
        public int SampleSize = 10;

        private CsvHelper.CsvReader csv;

        public string[] CurrentRecord { get { return csv.CurrentRecord; } }

        public bool Next()
        {
            return csv.Read();
        }
        
        public void Open(SetCsv table)
        {
            // Open file
            System.IO.StreamReader textReader = File.OpenText(table.FilePath);

            csv = new CsvHelper.CsvReader(textReader);

            csv.Configuration.HasHeaderRecord = table.HasHeaderRecord;
            csv.Configuration.Delimiter = table.Delimiter;
            csv.Configuration.CultureInfo = table.CultureInfo;
            csv.Configuration.Encoding = table.Encoding;

            // If header is present (parameter is true) then it will read first line and initialize column names from the first line (independent of whether these are names or values)
            // If header is not present (parameter is false) then it will position on the first line and make valid other structures. In particular, we can learn that column names are null.
            csv.Read();
        }

        public void Close()
        {
            if (csv == null) return;
            csv.Dispose();
            csv = null;
        }

        public List<string> GetColumns()
        {
            if (csv == null) return null;
            return csv.FieldHeaders.ToList();
        }

        public List<string[]> GetSampleValues()
        {
            var sampleRows = new List<string[]>();

            for (int row = 0; row < SampleSize; row++)
            {
                var rec = csv.CurrentRecord;
                sampleRows.Add(rec);

                if (!csv.Read()) break;
            }

            return sampleRows;
        }

    }

}
