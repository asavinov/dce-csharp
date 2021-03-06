﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.OleDb;
using System.Data;

using Rowid = System.Int32;

namespace Com.Schema.Rel
{
    /// <summary>
    /// Set with data loaded from a text file.
    /// </summary>
    public class SetTopText : SetTopOledb
    {
        private string _line; // The current line with values
        private string[] _columnNames; // A list of column names
        private string _fieldSeparator = ";";
        private string _fieldDelimiter = "\"";


        public SetTopText(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            DataSourceType = DataSourceType.CSV;

            // Do we need separate data types for CSV (text) files? 
            // Probably yes. But we can create ALL oledb types and then use only those suitable for texts
        }

    }

}
