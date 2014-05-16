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
    /// One operation is a unit of execution of the value engine. 
    /// Value operations are represented separately from set operations as expressions executed normally in a loop. 
    /// Value expressions are generated from a function definition, for example, from AST or some object representation. 
    /// </summary>
    public abstract class ValueOp
    {
        /**
         * Type of operation. What kind of operation has to be executed. It determines the meaning of operands and their processing.  
         */
        public ValueOpType OpType { get; set; }

        /**
         * Function, variable or another kind of run-time object used for the operation which has been resolved from the name.  
         */
        public string Name { get; set; } // It is used by resolver/binder and not used during interpretation.
        public Dim Action { get; set; } // It is the result of binding and is used during interpretation.
        public ActionType ActionType { get; set; }

        /**
         * It is a field representing the result returned by the node and to be used by the interpreter for next operations.
         */
        public object Value { get; set; }
        public Set ValueType { get; set; }


        /// <summary>
        /// Resolve names of accessors and types (all uses) to object references representing storage elements (dimensions or variables). 
        /// These dimensions and variables can be either in the local context (arguments and variables) or in the permanent context (schema). 
        /// The final result is that this expression can be executed without names by directing using the resolved objects. 
        /// </summary>
        public virtual void Resolve()
        {
            switch (OpType)
            {
                case ValueOpType.NOP:
                    {
                        break;
                    }
            }
        }

        /// <summary>
        /// Compute operation. The result is stored in the output field.
        /// Computations are performed without using the name by assuming that it has been already resolved.
        /// </summary>
        public virtual void Eval()
        {
            switch (OpType)
            {
                case ValueOpType.NOP:
                    {
                        break;
                    }
            }
        }

    }

    public enum ValueOpType
    {
        NOP, 

        SCOPE, // Operands are instructions to be executed sequentially. The scope might also store variables and other context objects. So it combines state and code.
        VARIABLE, // A named storage for one value that can be accessed by many other instructions (so it not an instruction but part of execution context)

        // All variables/arguments used in a program have to be allocated in advance and then binding/resolution has to be done for concrete context along with all needed variables/arguments
        ALLOC, // Allocate a variable in some context
        FREE, // Frees a variable from a context

        // Name is name of the function, variable, procedure, attribute (also a function).
        // Action is a run-time function or variable object (resolved from Name) that is used for calling.
        // Type is a set of the output result of the function or type of the variable
        // Value is the result of the operation (return value)
        CALL_FUNC, // Access to a function (1ChildNode -> ThisNode)
        CALL_VAR, // Access to a variable in some context (of type of ThisNode)
        CALL_PROC, // Call of a procedure

        // Name is attribute relative to the parent tuple. If the parent is not tuple then it is a name relative to the parent context, for example, parameter name. 
        // Action is a function with attribute Name (ParentNode -> ThisNode) that represents this constituent value relative to the parent 
        // Type is the set this tuple Value belongs to. Primitive or non-primitive. 
        // Value stores an id/surrogate of the (found) element in the set (primitive value or surrogate)
        VALUE,  // Node represents a primitive value (other than that it is equivalent to tuples)
        TUPLE,  // Combination of value/tuples. 

        // ??? How to represent computed values (rather than constants) in tuples and arguments, say, inputs of function calls? Are leaves VALUES, TUPLES, or directly some operation like PLUS
    }

    public enum ActionType
    {
        READ, // Read value from a variable/function or find a surrogate for a tuple
        WRITE, // Write value to a variable/function or append a tuple to a set (if not found)
        CALL, // Procedure call
    }

}
