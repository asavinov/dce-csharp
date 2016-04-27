using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Schema;
using Com.Data;

namespace Com.Schema.Rel
{
    /// <summary>
    /// Relational dimension representing a foreign key as a whole (without its attributes) or a primitive non-FK attribute. 
    /// </summary>
    public class ColumnRel : Column
    {
        /// <summary>
        /// Additional names specific to the relational model and imported from a relational schema. 
        /// </summary>
        public string RelationalFkName { get; set; } // The original FK name this dimension was created from

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Column

            json["RelationalFkName"] = RelationalFkName;
        }

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Column

            RelationalFkName = (string)json["RelationalFkName"];
        }

        #endregion

        public ColumnRel()
            : base(null, null, null)
        {
        }

        public ColumnRel(string name)
            : this(name, null, null)
        {
        }

        public ColumnRel(string name, DcTable input, DcTable output)
            : this(name, input, output, false, false)
        {
        }

        public ColumnRel(string name, DcTable input, DcTable output, bool isIdentity, bool isSuper)
            : base(name, input, output, isIdentity, isSuper)
        {
            _data = new ColumnDataEmpty();
            _data.Translate();
        }
    }

}
