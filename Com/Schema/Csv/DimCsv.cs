using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Schema.Csv
{
    /// <summary>
    /// Dimension representing a column in a text file. 
    /// </summary>
    public class DimCsv : Dim
    {
        /// <summary>
        /// Sample values. 
        /// </summary>
        public List<string> SampleValues { get; set; }

        public int ColumnIndex { get; set; } // Zero-based sequential column number in the file

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Dim

            json["ColumnIndex"] = ColumnIndex;
        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Dim

            ColumnIndex = (int)json["ColumnIndex"];
        }

        #endregion

        public DimCsv()
            : base(null, null, null)
        {
        }

        public DimCsv(string name)
            : this(name, null, null)
        {
        }

        public DimCsv(string name, DcTable input, DcTable output)
            : this(name, input, output, false, false)
        {
        }

        public DimCsv(string name, DcTable input, DcTable output, bool isIdentity, bool isSuper)
            : base(name, input, output, isIdentity, isSuper)
        {
            SampleValues = new List<string>();
            ColumnIndex = -1;
        }
    }

}
