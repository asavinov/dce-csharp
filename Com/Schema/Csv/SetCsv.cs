using System.Globalization;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    /// <summary>
    /// A table stored as a text file.
    /// </summary>
    public class SetCsv : Set
    {
        /// <summary>
        /// Complete file name for this table (where this table is stored).
        /// </summary>
        public string FilePath { get; set; }

        public string FileName { get { return Path.GetFileNameWithoutExtension(FilePath); } }

        //
        // Storage and format parameters for this table which determine how it is serialized
        //
        public bool HasHeaderRecord { get; set; }
        public string Delimiter { get; set; }
        public CultureInfo CultureInfo { get; set; }
        public Encoding Encoding { get; set; }

        public DcColumn[] GetColumnsByIndex() // Return an array of columns with indexes starting from 0 and ending with last index
        {
            var columns = new List<DcColumn>();
            int columnCount = 0;

            foreach (DcColumn col in Columns)
            {
                if (!(col is DimCsv)) continue;

                int colIdx = ((DimCsv)col).ColumnIndex;
                if (colIdx < 0) continue;

                if (colIdx >= columns.Count) // Ensure that this index exists 
                {
                    columns.AddRange(new DcColumn[colIdx - columns.Count + 1]);
                }

                columns[colIdx] = col;
                columnCount = Math.Max(columnCount, colIdx);
            }

            return columns.ToArray();
        }
        public string[] GetColumnNamesByIndex()
        {
            var columns = GetColumnsByIndex();
            var columnNames = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++) columnNames[i] = columns[i].Name;
            return columnNames;
        }

        #region ComTableData interface

        public override Rowid Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            return -1; // Not found 
        }

        public override bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
        {
            return true;
        }

        public override Rowid Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.OutputVariable.TypeTable == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");


            var columns = GetColumnsByIndex();
            string[] record = new string[columns.Length];

            //
            // Prepare a record with all fields. Here we choose the columns to be written
            //

            for (int i = 0; i < columns.Length; i++) // We must append one value to ALL greater dimensions even if a child expression is absent
            {
                DcColumn col = columns[i];
                ExprNode childExpr = expr.GetChild(col.Name);
                object val = null;
                if (childExpr != null) // Found. Value is present.
                {
                    val = childExpr.OutputVariable.GetValue();
                    if (val != null)
                    {
                        record[i] = val.ToString();
                    }
                    else
                    {
                        record[i] = "";
                    }
                }
            }

            // Really append record to the file
            ConnectionCsv conn = ((SchemaCsv)this.Schema).connection;
            conn.WriteNext(record);

            length++;
            return Length - 1;
        }

        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["file_path"] = FilePath;

            json["HasHeaderRecord"] = this.HasHeaderRecord;
            json["Delimiter"] = this.Delimiter;
            json["CultureInfo"] = this.CultureInfo.Name;
            json["Encoding"] = this.Encoding.EncodingName;
        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Set

            FilePath = (string)json["file_path"];

            HasHeaderRecord = (bool)json["HasHeaderRecord"];
            Delimiter = (string)json["Delimiter"];
            CultureInfo = new CultureInfo((string)json["CultureInfo"]);

            string encodingName = (string)json["Encoding"];
            if (string.IsNullOrEmpty(encodingName)) Encoding = System.Text.Encoding.Default;
            else if (encodingName.Contains("ASCII")) Encoding = System.Text.Encoding.ASCII;
            else if (encodingName == "Unicode") Encoding = System.Text.Encoding.Unicode;
            else if (encodingName.Contains("UTF-32")) Encoding = System.Text.Encoding.UTF32; // "Unicode (UTF-32)"
            else if (encodingName.Contains("UTF-7")) Encoding = System.Text.Encoding.UTF7; // "Unicode (UTF-7)"
            else if (encodingName.Contains("UTF-8")) Encoding = System.Text.Encoding.UTF8; // "Unicode (UTF-8)"
            else Encoding = System.Text.Encoding.Default;
        }

        #endregion

        #region Constructors and initializers.

        public SetCsv()
            : this("")
        {
        }

        public SetCsv(string name)
            : base(name)
        {
            HasHeaderRecord = true;
            Delimiter = ",";
            CultureInfo = System.Globalization.CultureInfo.CurrentCulture;
            Encoding = Encoding.UTF8;
        }

        #endregion
    }

}
