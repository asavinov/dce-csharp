﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcColumnData : DcJson // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        Rowid Length { get; set; }

        bool AutoIndex { get; set; }
        bool Indexed { get; }
        void Reindex();

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNull(Rowid input);

        object GetValue(Rowid input);
        void SetValue(Rowid input, object value);
        void SetValue(object value);

        void Nullify();

        void Append(object value);

        void Insert(Rowid input, object value);

        void Remove(Rowid input);

        //void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        //void InsertValue(Offset input, object value); // Changle length. Do we need this?

        //
        // Project/de-project
        //
        object Project(Rowid[] offsets);
        Rowid[] Deproject(object value);

        //
        // Typed methods for each primitive type like GetInteger(). No NULL management since we use real values including NULL.
        //

        //
        // Index control.
        //
        // bool IsAutoIndexed { get; set; } // Index is maintained automatically
        // void Index(); // Index all and build new index
        // void Index(Offset start, Offset end); // The specified interval has been changed (or inserted?)

        //
        // The former DcColumnDefinition 
        //

        /// <summary>
        /// Formula in COEL with the function definition
        /// </summary>
        string Formula { get; set; }

        /// <summary>
        /// Source (user, non-executable) formula for computing this function consisting of value-operations
        /// </summary>
        //AstNode FormulaAst { get; set; }

        //
        // Structured (object) representation
        //

        /// <summary>
        /// Whether output values are appended to the output set. 
        /// </summary>
        bool IsAppendData { get; set; }

        bool IsAppendSchema { get; set; }

        /// <summary>
        /// Represents a function definition in terms of other functions (select expression).
        /// When evaluated, it computes a value of the greater set for the identity value of the lesser set.
        /// For aggregated columns, it is an updater expression which computes a new value from the current value and a new fact (measure).
        /// </summary>
        //ExprNode FormulaExpr { get; set; }

        //
        // Compute. Data operations.
        //

        void Evaluate();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        bool IsUpToDate { get; set; }

        List<DcTable> UsesTables(bool recursive); // This element depends upon
        List<DcColumn> UsesColumns(bool recursive); // This element depends upon

        List<DcTable> IsUsedInTables(bool recursive); // Dependants
        List<DcColumn> IsUsedInColumns(bool recursive); // Dependants
    }

}
