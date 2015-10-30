using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Utils;
using Com.Schema;
using Com.Data.Query;

namespace Com.Data
{
    public interface DcColumnDefinition // How a function is represented and evaluated. It uses API of the column storage like read, write (typed or untyped).
    {
        // The form of representation:
        // Our own v-expr or its parsed AST: 
        // Native source code: Java, C# etc.
        // Native library class: Java, C#, Python etc.
        // OS script (e.g., using pipes): Bash, Python etc.

        // Type of formula:
        // Primitive (direct computations returning a primitive value): = [col 1]+this.[col 2] / SYS_FUNC([col 3].[col 4] - 1.0)
        // Complex (mapping, tuple): ([Set 1] [s 1] = this.[a1], [Set 2] [s 2] = (...), [Double] [amount] = [col1]+[col2] )
        // A sequence of statements with return (primitive or tuple).
        // Aggregation/accumulation (loop over another set): standard (sum, mul etc. - separate class), user-defined like this+value/2 - 1.
        // Join predicate (two loops over input and output sets)


        //
        // COEL (language) representation
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
        ExprNode FormulaExpr { get; set; }

        //
        // Compute. Data operations.
        //

        void Evaluate();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //

        List<DcTable> UsesTables(bool recursive); // This element depends upon
        List<DcTable> IsUsedInTables(bool recursive); // Dependants

        List<DcColumn> UsesColumns(bool recursive); // This element depends upon
        List<DcColumn> IsUsedInColumns(bool recursive); // Dependants
    }

}
