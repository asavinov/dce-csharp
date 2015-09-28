using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        #endregion
    }

}
