using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Data
{
    /// <summary>
    /// Empty data.
    /// 
    /// </summary>
    public class DimDataEmpty : DcColumnData
    {

        #region ComColumnData interface

        protected Rowid _length;
        public Rowid Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        public bool AutoIndex { get; set; }
        protected bool _indexed;
        public bool Indexed { get { return _indexed; } }
        public void Reindex() { }

        public bool IsNull(Rowid input) { return true; }

        public object GetValue(Rowid input) { return null; }

        public void SetValue(Rowid input, object value) { }
        public void SetValue(object value) { }

        public void Nullify() { }

        public void Append(object value) { }

        public void Insert(Rowid input, object value) { }

        public void Remove(Rowid input) { }

        public object Project(Rowid[] offsets) { return null; }

        public Rowid[] Deproject(object value) { return null; } // Or empty array 

        protected DcColumnDefinition _definition;
        public virtual DcColumnDefinition GetDefinition() { return _definition; }

        #endregion

        #region DcJson serialization

        public virtual void ToJson(JObject json) // Write fields to the json object
        {
            // No super-object

            // Column definition
            if (GetDefinition() != null)
            {
                JObject columnDef = new JObject();

                columnDef["generating"] = GetDefinition().IsAppendData ? "true" : "false";
                //columnDef["definition_type"] = (int)Definition.DefinitionType;

                if (GetDefinition().FormulaExpr != null)
                {
                    columnDef["formula"] = Com.Schema.Utils.CreateJsonFromObject(GetDefinition().FormulaExpr);
                    GetDefinition().FormulaExpr.ToJson((JObject)columnDef["formula"]);
                }

                json["definition"] = columnDef;
            }

        }
        public virtual void FromJson(JObject json, DcWorkspace ws) // Init this object fields by using json object
        {
            // No super-object

            // Column definition
            JObject columnDef = (JObject)json["definition"];
            if (columnDef != null && GetDefinition() != null)
            {
                GetDefinition().IsAppendData = columnDef["generating"] != null ? StringSimilarity.JsonTrue(columnDef["generating"]) : false;
                //Definition.DefinitionType = columnDef["definition_type"] != null ? (DcColumnDefinitionType)(int)columnDef["definition_type"] : DcColumnDefinitionType.FREE;

                if (columnDef["formula"] != null)
                {
                    ExprNode node = (ExprNode)Com.Schema.Utils.CreateObjectFromJson((JObject)columnDef["formula"]);
                    if (node != null)
                    {
                        node.FromJson((JObject)columnDef["formula"], ws);
                        GetDefinition().FormulaExpr = node;
                    }
                }

            }

        }

        #endregion

    }

}
