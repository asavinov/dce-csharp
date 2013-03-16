using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// Set with data loaded from OData connection.
    /// </summary>
    public abstract class SetRootOdata : SetRoot
    {
        /// <summary>
        /// Connection to the remote database or engine where data is stored and processed.
        /// It can be a file name or url so it depends on the implementation and is supposed to be used in extensions.
        /// </summary>
        string _connection;

        private string _fileName; // Equivalent to the concrete class
        private string _line; // The current line with values
        private string[] _columnNames; // A list of column names
        private string _fieldSeparator;

        public override void Populate()
        {
			// Open file
			// Load all rows (the first row has column names)
	    }

        public void next()
        {
			// Move to the next row
	    }

        public void getValue(string column)
        {
	    }

        public SetRootOdata(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
        }

        #region Deprecated 
/*
        public override Set GetPrimitiveSet(Attribute attribute)
        {
            string typeName = "string";

            // Type mapping
            switch (attribute.DataType)
            {
                case "Double": // System.Data.OleDb.OleDbType.Double
                    break;
                case "Integer": // System.Data.OleDb.OleDbType.Integer
                    break;
                case "Char": // System.Data.OleDb.OleDbType.Char
                case "VarChar": // System.Data.OleDb.OleDbType.VarChar
                case "VarWChar": // System.Data.OleDb.OleDbType.VarWChar
                case "WChar": // System.Data.OleDb.OleDbType.WChar
                    break;
                default:
                    // All the rest of types or error in the case we have enumerated all of them
                    break;
            }

            Set set = GetPrimitiveSet(typeName); // Find primitive set with this type
            return set;
        }
*/
        #endregion
    }

}
