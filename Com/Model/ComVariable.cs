using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    public interface ComVariable // It is a storage element like function or table
    {
        //
        // Variable name (strictly speaking, it should belong to a different interface)
        //

        string Name { get; set; }

        //
        // Type info
        //

        string TypeName { get; set; }
        ComTable TypeTable { get; set; } // Resolved table name

        //
        // Variable data. Analogous to the column data interface but without input argument
        //

        bool IsNull();

        object GetValue();
        void SetValue(object value);

        void Nullify();

        //
        // Typed methods
        //

    }

    public class Variable : ComVariable
    {
        protected bool isNull;
        object Value;


        #region ComVariable interface

        public string Name { get; set; }

        public string TypeName { get; set; }
        public ComTable TypeTable { get; set; }

        public bool IsNull()
        {
            return isNull;
        }

        public object GetValue()
        {
            return isNull ? null : Value;
        }

        public void SetValue(object value)
        {
            if (value == null)
            {
                Value = null;
                isNull = true;
            }
            else
            {
                Value = value;
                isNull = false;
            }
        }

        public void Nullify()
        {
            isNull = true;
        }

        #endregion 

        public Variable(string name, string type)
        {
            Name = name;
            TypeName = type;

            isNull = true;
            Value = null;
        }

        public Variable(string name, ComTable type)
        {
            Name = name;
            TypeName = type.Name;
            TypeTable = type;

            isNull = true;
            Value = null;
        }
    }

}
