using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Data;

namespace Com.Schema.Csv
{
    /// <summary>
    /// Dimension representing a column in a text file. 
    /// </summary>
    public class ColumnCsv : Column
    {
        /// <summary>
        /// Sample values. 
        /// </summary>
        public List<string> SampleValues { get; set; }

        public int ColumnIndex { get; set; } // Zero-based sequential column number in the file

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Column

            json["ColumnIndex"] = ColumnIndex;
        }

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Column

            ColumnIndex = (int)json["ColumnIndex"];
        }

        #endregion

        public ColumnCsv()
            : base(null, null, null)
        {
        }

        public ColumnCsv(string name)
            : this(name, null, null)
        {
        }

        public ColumnCsv(string name, DcTable input, DcTable output)
            : this(name, input, output, false, false)
        {
        }

        public ColumnCsv(string name, DcTable input, DcTable output, bool isIdentity, bool isSuper)
            : base(name, input, output, isIdentity, isSuper)
        {
            SampleValues = new List<string>();
            ColumnIndex = -1;

            _data = new ColumnDataEmpty();
        }
    }

}
