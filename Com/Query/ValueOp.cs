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
    public class ValueOp : TreeNode<ValueOp>
    {
        /**
         * Type of operation. What kind of operation has to be executed. It determines the meaning of operands and their processing.  
         */
        public ValueOpType OpType { get; set; }

        /**
         * Function, variable or another kind of run-time object used for the operation which has been resolved from the name.  
         */
        public string Name { get; set; } // It is used by resolver/binder and not used during interpretation.
        public Dim Action { get; set; } // It is the result of binding and this run-time object is used during execution.
        public ActionType ActionType { get; set; } // Read, write, call etc. 

        /**
         * It is a field representing the result returned by the node and to be used by the interpreter for next operations.
         */
        public object Value { get; set; }
        public Set ValueType { get; set; }

        /// <summary>
        /// Resolve names of accessors and types (all uses) to object references representing storage elements (dimensions or variables). 
        /// These dimensions and variables are searched for in the parent contexts (either among arguments and variables or in the schema). 
        /// The final result is that this expression can be executed without names by directing using the resolved objects. 
        /// Also, we can resolve and check correctness of types.
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
        /// Compute operation. The result is stored in the output field or in a context object.
        /// Computations are performed without using names by assuming that they have been already resolved.
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

        public ValueOp()
        {
            OpType = ValueOpType.NOP;
        }

        public ValueOp(ValueOpType opType)
        {
            OpType = opType;
        }
    }

    public enum ValueOpType
    {
        NOP,

        CONTEXT, // It is a context/scope object with execution state. Operands are instructions to be executed sequentially. 

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
        TUPLE,  // Combination of value/tuples. Basically, it should be treated as a (complex) value.

        // ??? How to represent computed values (rather than constants) in tuples and arguments, say, inputs of function calls? Are leaves VALUES, TUPLES, or directly some operation like PLUS
    }

    public enum ActionType
    {
        READ, // Read value from a variable/function or find a surrogate for a tuple
        WRITE, // Write value to an existing variable/function (do nothing if it does not exist)
        UPDATE, // Update value by applying some operation
        APPEND, // Append a value if it does not exist and write it if does exist. The same as write except that a new element can be added
        INSERT, // The same as append except that a position is specified
        CALL, // Procedure call
    }

    /// <summary>
    /// It combines code (child operations) with state (variables).
    /// A context can be used as a (nested) scope. 
    /// It can be also used as a procedure body by including arguments as variables and return value. Name is procedure name.
    /// </summary>
    public class ValueContext : ValueOp
    {
        public List<SetTop> Schemas { get; set; } // State. Each schema stores a list of function objects as well as sets and maybe also schema-level variables.

        public List<ContextVariable> Variables { get; set; } // State. A list of named and typed variables each storing a shared run-time object references that can be used by operations within this context. 

        public ValueContext()
        {
            OpType = ValueOpType.CONTEXT;

            Schemas = new List<SetTop>();

            Variables = new List<ContextVariable>();
        }
    }

}
