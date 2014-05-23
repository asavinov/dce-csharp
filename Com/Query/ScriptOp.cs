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

        ASSIGN, // Copy some content to an existing variable. The content is normally returned from some call or expression but can be also context from another variable (which is also an expression).
        
        //
        // Create new set object. Result is a set object.
        //

        // Operands: function name or definition, source set (can be another set-creation operation like projection or product)
        // Result is a new set reference (not populated)
        PROJECTION, // Create/define a new set using projection operation
        // Result is a new set reference (not populated)
        PRODUCT, // Create/define a new set as product of other sets. Its sets belong to schema or context?

        //
        // Create new function object. Result is a function object.
        //
        
        FUNCTION, // Create/define a new function. In local context or in schema? 

        //
        // Evalute function(s) or set(s). Result is new/changed data arrays (in functions) and set sizes.
        //

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
    /// Named and typed variable that stores a run-time value or a named reference to a value.
    /// </summary>
    public class ContextVariable
    {
        // We might need to store a parent or another reference to the global context
        // For example, a schema, a set or an execution context could maintain lists of variables. 
        // It is useful for resolution of other names as a method applied to this element and starting from this context.
        // In other words, it is useful to have a connected structure of all contexts and scopes where names are relative identifiers of some content/state. 
        // For example, an argument of an instruction (code) could reference some variable by-name but finding this variable will require knowing the context object attached to the code.

        public VariableClass Class { get; set; } // Kind of object represented by the variable like Schema, Set, Function, Value, Tuple, Array etc.
        public bool ValueIsReference { get; set; } // If true then the value stores a reference (name) to storage element where the value is stored and have to be retrieved from. 
        // Otherwise, the value field stores a run-time object that has to be used without further resolution and directly corresponds to the specified type. 
        // Alternatively, we can introduce REFERENCE type (along with SET, DIM etc.) but then type information is represented only in the last storage element which really stores the value, which however might not be evailable and hence it will be difficult to resolve this reference without having its type.

        public string TypeName { get; set; } // Name of the set the represented element is a member in. 
        public Set Type { get; set; } // Set object the represented element is a member in. 
        // For values it is a concrete set, for functions it is the output set, for sets it is the set itself.

        public string Name { get; set; } // Name of the variable. It is also used as a role relative to the context where it is used like tuple or argument list. For example, it could be 'return' or 'this'.
        // This is relative name without prefix. The prefix is supposed to be defined by the context object. For execution context, the prefix is 'this'.

        public object Value { get; set; } // Content having the name of this variable and its type. It can a name to another variable or real content like run-time reference to a set, dimension, schema or value. It also can be also any data that is used by the interpreter like AST with a functino body definition as a value-expression. 
        // If it is a name (reference), then it can be a fully-qualified name with prefix like 'this:myVar' or 'My Schema:My Set:My Dimension'.

        public ContextVariable(VariableClass typeName, string name, object content)
            : this(typeName, name)
        {
            Value = content;
        }

        public ContextVariable(VariableClass typeName, string name)
            : this(name)
        {
            Class = typeName;
        }

        public ContextVariable(string name)
        {
            Name = name;
            ValueIsReference = false;
        }
    }

    public enum VariableClass
    {
        NONE, // Not set
        SET, // The variable stores a reference to a set object
        DIM, // The variable stores a reference to a dimension object
        VAL, // The variable stores a value
        TUPLE, // The variable stores a tuple (complex value)
    }

}
