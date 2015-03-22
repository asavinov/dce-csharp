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

    /// <summary>
    /// Access to a (text) file. Connection is specified by file name.
    /// </summary>
    public class ConnectionCsv
    {
        // Various parameters for reading the file:
        public int SampleSize = 10;

        private CsvHelper.CsvReader csvReader;

        public string[] CurrentRecord { get { return csvReader.CurrentRecord; } }

        public void OpenReader(SetCsv table)
        {
            // Open file
            System.IO.StreamReader textReader = File.OpenText(table.FilePath);
            //System.IO.StreamReader textReader = new StreamReader(table.FilePath, table.Encoding);

            csvReader = new CsvHelper.CsvReader(textReader);

            csvReader.Configuration.HasHeaderRecord = table.HasHeaderRecord;
            csvReader.Configuration.Delimiter = table.Delimiter;
            csvReader.Configuration.CultureInfo = table.CultureInfo;
            csvReader.Configuration.Encoding = table.Encoding;

            // If header is present (parameter is true) then it will read first line and initialize column names from the first line (independent of whether these are names or values)
            // If header is not present (parameter is false) then it will position on the first line and make valid other structures. In particular, we can learn that column names are null.
            csvReader.Read();
        }

        public void CloseReader()
        {
            if (csvReader == null) return;
            csvReader.Dispose();
            csvReader = null;
        }

        public bool ReadNext()
        {
            return csvReader.Read();
        }

        public List<string> ReadColumns()
        {
            if (csvReader == null) return null;
            if (csvReader.Configuration.HasHeaderRecord && csvReader.FieldHeaders != null)
            {
                return csvReader.FieldHeaders.ToList();
            }
            else // No columns
            {
                var names = new List<string>();
                var rec = csvReader.CurrentRecord;
                for (int f = 0; f < rec.Length; f++)
                {
                    names.Add("Column " + (f+1));
                }
                return names;
            }
        }

        public List<string[]> ReadSampleValues()
        {
            var sampleRows = new List<string[]>();

            for (int row = 0; row < SampleSize; row++)
            {
                var rec = csvReader.CurrentRecord;
                sampleRows.Add(rec);

                if (!csvReader.Read()) break;
            }

            return sampleRows;
        }

        private CsvHelper.CsvWriter csvWriter;

        public void OpenWriter(SetCsv table)
        {
            // Open file
            //System.IO.StreamWriter textWriter = File.OpenWrite(table.FilePath);
            System.IO.StreamWriter textWriter = new StreamWriter(table.FilePath, false, table.Encoding);

            csvWriter = new CsvHelper.CsvWriter(textWriter);

            csvWriter.Configuration.HasHeaderRecord = table.HasHeaderRecord;
            csvWriter.Configuration.Delimiter = table.Delimiter;
            csvWriter.Configuration.CultureInfo = table.CultureInfo;
            csvWriter.Configuration.Encoding = table.Encoding;

            csvWriter.Configuration.QuoteAllFields = true;

        }

        public void CloseWriter()
        {
            if (csvWriter == null) return;
            csvWriter.Dispose();
            csvWriter = null;
        }

        public void WriteNext(string[] record)
        {
            //csvWriter.WriteRecord<string[]>(record);
            for (int i = 0; i < record.Length; i++)
            {
                string val = "";
                if (record[i] != null) val = record[i];

                csvWriter.WriteField(val);
            }

            csvWriter.NextRecord();
        }

    }

}
