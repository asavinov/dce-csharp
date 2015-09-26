using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Schema;

namespace Com.Schema.Rel
{
    /// <summary>
    /// Relational dimension representing a foreign key as a whole (without its attributes) or a primitive non-FK attribute. 
    /// </summary>
    public class DimRel : Dim
    {
        /// <summary>
        /// Additional names specific to the relational model and imported from a relational schema. 
        /// </summary>
        public string RelationalFkName { get; set; } // The original FK name this dimension was created from

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Dim

            json["RelationalFkName"] = RelationalFkName;
        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Dim

            RelationalFkName = (string)json["RelationalFkName"];
        }

        #endregion

        public DimRel()
            : base(null, null, null)
        {
        }

        public DimRel(string name)
            : this(name, null, null)
        {
        }

        public DimRel(string name, DcTable input, DcTable output)
            : this(name, input, output, false, false)
        {
        }

        public DimRel(string name, DcTable input, DcTable output, bool isIdentity, bool isSuper)
            : base(name, input, output, isIdentity, isSuper)
        {
        }
    }

}
