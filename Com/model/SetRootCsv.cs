using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Set with data loaded from a CSV file.
    /// </summary>
    public abstract class SetRootCsv : SetRoot
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

        public void Populate()
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

        public SetRootCsv(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
        }
    }

}
