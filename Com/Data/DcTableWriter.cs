using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Utils;
using Com.Schema;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcTableWriter
    {
        void Open();
        void Close();

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

        /// <summary>
        /// Analyze this definition by extracting the structure of its function output. 
        /// Append these output columns from the definition of the output table. 
        ///
        /// The method guarantees that the function outputs (data) produced by this definition can be appended to the output table, that is, the output table is consistent with this definition. 
        /// This method can be called before (or within) resolution procedure which can be viewed as a schema-level analogue of data population and which ensures that we have correct schema which is consistent with all formulas/definitions. 
        /// </summary>
        //object Append(); // Null if not appended
    }

}
