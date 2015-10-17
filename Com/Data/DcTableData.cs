using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcTableData // It is interface for manipulating data in a table.
    {
        Rowid Length { get; set; }

        bool AutoIndex { set; }
        bool Indexed { get; }
        void Reindex();

        //
        // Tuple (flat record) methods: append, insert, remove, read, write.
        //
        // Here we use TUPLE and its constituents as primitive types: Reference etc.
        // Column names or references are important. Types (table references or names) are necessary and important. Maybe also flags like Super, Key would be useful. 
        // TUPLE could be used as a set structure specification (e.g., param for set creation).
        //

        Rowid Find(DcColumn[] dims, object[] values);
        Rowid Append(DcColumn[] dims, object[] values);
        void Remove(int input);
    }

}
