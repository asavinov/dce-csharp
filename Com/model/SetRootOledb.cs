using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Set with data loaded using OleDb.
    /// </summary>
    public abstract class SetRootOleDb : SetRoot
    {
        /// <summary>
        /// Connection to the remote database or engine where data is stored and processed.
        /// </summary>
        string _connection;

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

        public SetRootOleDb(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
        }
    }

}
