using System.Globalization;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Data;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    /// <summary>
    /// A table stored as a text file.
    /// </summary>
    public class TableCsv : Table
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

        #region DcTableData interface

        public override DcTableReader GetTableReader()
        {
            return new TableReaderCsv(this);
        }

        public override DcTableWriter GetTableWriter()
        {
            return new TableWriterCsv(this);
        }

        #endregion

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Table

            json["file_path"] = FilePath;

            json["HasHeaderRecord"] = this.HasHeaderRecord;
            json["Delimiter"] = this.Delimiter;
            json["CultureInfo"] = this.CultureInfo.Name;
            json["Encoding"] = this.Encoding.EncodingName;
        }

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Table

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

        public TableCsv(DcSpace space)
            : this("", space)
        {
        }

        public TableCsv(string name, DcSpace space)
            : base(name, space)
        {
            HasHeaderRecord = true;
            Delimiter = ",";
            CultureInfo = System.Globalization.CultureInfo.CurrentCulture;
            Encoding = Encoding.UTF8;
        }

        #endregion
    }

}
