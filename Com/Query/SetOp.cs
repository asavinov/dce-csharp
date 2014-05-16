using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Com.Model;

using Offset = System.Int32;

namespace Com.Query
{
    /// <summary>
    /// One operation is a unit of execution of the set engine.
    /// </summary>
    public abstract class SetOp
    {
        /**
         * Type of operation. What kind of operation has to be executed. It determines the meaning of operands and their processing.  
         */
        public SetOpType OpType { get; set; }

    }

    public enum SetOpType
    {
        SCOPE, // Operands are instructions to be executed sequentially. The scope might also store variables and other context objects.

        // All variables/arguments used in a program have to be allocated in advance and then binding/resolution has to be done for concrete context along with all needed variables/arguments
        ALLOC, // Allocate a set variable in some context
        FREE, // Frees a set variable from a context

        // Operands: function name or definition, source set (can be another set-creation operation like projection or product)
        // Result is a new set reference (not populated)
        PROJECTION, // Create/define a new set using projection operation
        // Result is a new set reference (not populated)
        PRODUCT, // Create/define a new set as product of other sets. Its sets belong to schema or context?

        FUNCTION, // Create/define a new function. In local context or in schema? 

        // Result is a populated function
        EVAL_FUNC, // Evaluate a function in a loop by interpreting its body (value-expression) and storing the output
        EVAL_AGGR, // Evaluate a function in a loop on fact by updating its existing values
        EVAL_SET, // Populate a set by producing all its identity (free) functions. 
    }
}
