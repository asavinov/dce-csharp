using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Data.Query;
using Com.Data.Eval;

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
        // Value methods (convenience, probably should be removed and replaced by manual access to dimensions)
        //

        object GetValue(string name, Rowid offset);
        void SetValue(string name, Rowid offset, object value);

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

        //
        // Expression (nested record) methods: append, insert, remove, read, write.
        //

        Rowid Find(ExprNode expr);
        bool CanAppend(ExprNode expr);
        Rowid Append(ExprNode expr);

        //Offset FindTuple(CsRecord record); // If many records can satisfy then another method has to be used. What is many found? Maybe return negative number with the number of records (-1 or Length means not found, positive means found 1)? 
        //void InsertTuple(Offset input, CsRecord record); // All keys are required? Are non-keys allowed?
        //void RemoveTuple(Offset input); // We use it to remove a tuple that does not satisfy filter constraints. Note that filter constraints can use only data that is loaded (some columns will be computed later). So the filter predicate has to be validated for sets which are projected/loaded or all other functions have to be evaluated during set population. 

        //
        // Typed data manipulation methods (do we need this especially taking into account that we will mapping of data types with conversion)
        // Here we need an interface like ResultSet in JDBC with all possible types
        // Alternative: maybe define these methos in the SetRemote class where we will have one class for manually entering elements
        //
    }

}
