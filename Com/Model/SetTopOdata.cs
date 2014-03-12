using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// Set with data loaded from OData connection.
    /// </summary>
    public class SetTopOdata : SetTop
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

        public SetTopOdata(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
        }

    }

}
