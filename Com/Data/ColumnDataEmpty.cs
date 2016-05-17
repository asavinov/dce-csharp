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
    public class ColumnDataEmpty : DcColumnData
    {

        #region DcColumnData interface

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

        #endregion

        #region The former DcColumnDefinition. Now part of DcColumnData

        public string Formula { get; set; }

        public bool IsAppendSchema { get; set; }
        public bool HasValidSchema
        {
            get { return true; }
            set {; }
        }
        public void Translate() { }

        public bool IsAppendData { get; set; }
        public bool HasValidData
        {
            get { return true; }
            set {; }
        }
        public void Evaluate() { }

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        public List<DcColumn> UsesColumns() { return null; } // This element depends upon
        public List<DcTable> UsesTables() { return null; } // Dependants

        public List<DcColumn> IsUsedInColumns() { return null; } // This element depends upon
        public List<DcTable> IsUsedInTables() { return null; } // Dependants

        #endregion

        #region DcJson serialization

        public virtual void ToJson(JObject json) // Write fields to the json object
        {
            // No super-object

            // Column definition
        }
        public virtual void FromJson(JObject json, DcSpace ws) // Init this object fields by using json object
        {
            // No super-object

            // Column definition
        }

        #endregion

    }

}
