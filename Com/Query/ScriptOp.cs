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
    public class ScriptOp : TreeNode<ScriptOp>
    {
        /**
         * Type of operation. What kind of operation has to be executed. It determines the meaning of operands and their processing.  
         */
        public ScriptOpType OpType { get; set; }

        public ScriptOp()
        {
            OpType = ScriptOpType.NOP;
        }

        public ScriptOp(ScriptOpType opType)
        {
            OpType = opType;
        }
    }

    public enum ScriptOpType
    {
        NOP, // Not initialized.

        CONTEXT, // It is a context/scope object with execution state.

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

    /// <summary>
    /// It combines code (child operations) with state (variables).
    /// A context can be used as a (nested) scope. 
    /// It can be also used as a procedure body by including arguments as variables and return value. 
    /// </summary>
    public class ScriptContext : ScriptOp
    {
        public List<SetTop> Schemas { get; set; } // State. Each schema stores a list of function objects as well as sets and maybe also schema-level variables.

        public List<ContextVariable> Variables { get; set; } // State. A list of named and typed variables each storing a shared run-time object references that can be used by operations within this context. 

        public ScriptContext()
        {
            OpType = ScriptOpType.CONTEXT;

            Schemas = new List<SetTop>();

            Variables = new List<ContextVariable>();
        }
    }

    /// <summary>
    /// Named and typed variable that stores a reference to a run-time object that can be used during execution.
    /// </summary>
    public class ContextVariable
    {
        public string Name { get; set; } // Name of the variable
        public object Content { get; set; } // Run-time object referenced by the variable like concrete set, function or value.

        public string TypeName { get; set; } // Type name
        public Set Type { get; set; } // Type of the value(s) corresponding to the referenced object. For values it is a concrete set, for functions it is the return set, for sets it is not used (or is equal to the set itself).

        public ContextVariable(string typeName, string name, object content)
            : this(typeName, name)
        {
            Content = content;
        }
        
        public ContextVariable(string typeName, string name)
            : this(name)
        {
            TypeName = typeName;
        }

        public ContextVariable(string name)
        {
            Name = name;
        }
    }

    public enum VariableType
    {
        NONE, // Not set
        SET, // The variable stores a reference to a set object
        DIM, // The variable stores a reference to a dimension object
        VAL, // The variable stores a value
        TUPLE, // The variable stores a tuple (complex value)
    }

}
