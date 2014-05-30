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
         * Type of operation/action. What kind of operation has to be executed. It determines the meaning of operands and their processing.  
         */
        public ScriptOpType OpType { get; set; }

        /**
         * Function, variable or another kind of run-time object used for the operation which has been resolved from the name.  
         */
        public string Name { get; set; } // Action/accessor name relative to the execution context. 
        public object Action { get; set; } // Action/accessor run-time object resolved from the name. It can be a context variable, dimension, set etc.

        /**
         * Run-time result of the action which is returned by the node and consumed by the parent node or next node.
         */
        public ContextVariable Result { get; set; }

        /// <summary>
        /// Evaluate one instruction. Change the state in the (parent) context depending on its current state.
        /// Names are resolved for each instruction. Alternatively, we could first resolve all names in all instructions in a separate pass and then execute without resolution.
        /// </summary>
        public virtual object Execute() 
        {
            // Context stores the current state which is read and then written as a result of the operation execution
            ScriptContext ctx = (ScriptContext)Parent;
            string name;

            switch (OpType)
            {
                case ScriptOpType.NOP: 
                    break;

                case ScriptOpType.CONTEXT: break; // Not possible: we overload this type of nodes

                case ScriptOpType.ALLOC:
                    name = ""; // TODO: read name (and type) of the new variable
                    ctx.AllocVariable(name);
                    break;
                case ScriptOpType.FREE:
                    name = ""; // TODO: read name of the variable
                    ctx.FreeVariable(name);
                    break;

                case ScriptOpType.WRITE: // ASSIGN
                    break;
            }

            return null; // TODO: Return content of the 'return' variable
        }

        public ScriptOp(ScriptOpType opType, string name)
            : this(opType)
        {
            Name = name;
        }

        public ScriptOp(ScriptOpType opType)
            : this()
        {
            OpType = opType;
        }

        public ScriptOp()
        {
            OpType = ScriptOpType.NOP;
            Result = new ContextVariable("return");
        }
    }

    public enum ScriptOpType
    {
        NOP, // No action
        CONTEXT, // Context/scope. Operands are instructions to be executed sequentially. The class is overloaded to include context.

        ALLOC, // Create a new variable
        FREE, // Delete an existing variable

        VALUE, // Stores directly the result in code (in instruction), e.g., parameter by-value in source code including tuples. No action is performed (but the value has to be transformed into the run-time representation).
        TUPLE, // Stores directly the result in code (in instruction), e.g., parameter by-value in source code including tuples. No action is performed (but the value has to be transformed into the run-time representation).

        DOT, // Essentially, the same as CALL but the first argument is named 'this' and has special interpretation
        CALL, // Generic procedure call including system calls, arithmetic operations etc.
        READ, // Read accossor or getter. Read value from a variable/function or find a surrogate for a tuple. Normally used in method parameters.
        WRITE, // Assignment, write accessor or setter. Write value to an existing variable/function (do nothing if it does not exist). Normally is used for assignment. 
        UPDATE, // Update value by applying some operation. Normally is used for affecting directly a target rather than loading it, changing and then storing.
        APPEND, // Append a value if it does not exist and write it if does exist. The same as write except that a new element can be added
        INSERT, // The same as append except that a position is specified

        //
        // ??? Special action types specific to the syntax of COEL - DO WE NEED THIS in executable code or translator will convert everything to normal API???
        //

        // Result is a new set reference (not populated)
        PRODUCT, // Create/define a new set as product of other sets. Its sets belong to schema or context?
        // Operands: function name or definition, source set (can be another set-creation operation like projection or product)
        // Result is a new set reference (not populated)
        PROJECTION, // Create/define a new set using projection operation
        DEPROJECTION,

        FUNCTION, // Create/define a new function. In local context or in schema? 
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
        public ContextVariable GetVariable(string name) 
        {
            return Variables.FirstOrDefault(n => n.Name == name);
        }
        public ContextVariable AllocVariable(string name)
        {
            ContextVariable var = GetVariable(name);
            if (var != null) return var;

            var = new ContextVariable(name);
            Variables.Add(var);
            return var;
        }
        public ContextVariable FreeVariable(string name)
        {
            ContextVariable var = GetVariable(name);
            if (var == null) return null;

            Variables.Remove(var);
            return var;
        }

        public override object Execute() // The same as evaluate
        {
            foreach (ScriptOp op in Children)
            {
                op.Execute();
            }

            return null; // TODO: Return content of the 'return' variable
        }

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

        public string TypeName { get; set; } // Name of the set the represented element is a member in. 
        public Set Type { get; set; } // Set object the represented element is a member in. 
        // For values it is a concrete set, for functions it is the output set, for sets it is the set itself.

        public string Name { get; set; } // Name of the variable. It is also used as a role relative to the context where it is used like tuple or argument list. For example, it could be 'return' or 'this'.
        // This is relative name without prefix. The prefix is supposed to be defined by the context object. For execution context, the prefix is 'this'.

        public object Value { get; set; } // Content having the name of this variable and its type. It can a name to another variable or real content like run-time reference to a set, dimension, schema or value. It also can be also any data that is used by the interpreter like AST with a functino body definition as a value-expression. 
        // If it is a name (reference), then it can be a fully-qualified name with prefix like 'this:myVar' or 'My Schema:My Set:My Dimension'.

        public ContextVariable(VariableClass varClass, string name, object content)
            : this(varClass, name)
        {
            Value = content;
        }

        public ContextVariable(VariableClass varClass, string name)
            : this(name)
        {
            Class = varClass;
        }

        public ContextVariable(string name)
        {
            Name = name;
        }
    }

    public enum VariableClass
    {
        NONE, // Not set
        SET, // The variable stores a reference to a set object
        DIM, // The variable stores a reference to a dimension object
        VAL, // The variable stores a value
        TUPLE, // The variable stores a tuple (complex value) - DO WE NEED THIS?
        AST, // The variable stores an ast tree - DO WE NEED THIS?
    }

}
