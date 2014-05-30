using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Com.Model;

using Offset = System.Int32;

namespace Com.Query
{
    public class AstNode : TreeNode<AstNode>
    {
        /// <summary>
        /// Type of syntactic node which corresponds to a syntactic rule.
        /// </summary>
        public AstRule Rule { get; set; }

        /// <summary>
        /// It is normally AST leaf node. Its content is normally not parsed. 
        /// It can represent names of variables, set members, annotations as well as primitive values and literals.
        /// </summary>
        public string Name { get; set; }

        public AstNode GetChild(int child) { return (AstNode)Children[child]; }

        /// <summary>
        /// Generate an executable program from this script.
        /// </summary>
        public ScriptContext Translate()
        {
            if (Rule != AstRule.SCRIPT) return null; // Wrong use

            List<ScriptOp> instructions = TranslateNode();

            // Simply copy all generated instructions. In future, we will optimize this sequence and maybe create a graph of operations
            ScriptContext scriptCtx = new ScriptContext();
            foreach(ScriptOp op in instructions) 
            {
                scriptCtx.AddChild(op);
            }

            return scriptCtx;
        }

        /// <summary>
        /// Analyze this syntactic node and generate instructions in the script context.
        /// </summary>
        public List<ScriptOp> TranslateNode()
        {
            // What do we need for scripts?
            // Normal expressions with typed variables and call expressions and some special COEL syntactic rules like PRODUCT and PROJECTION.
            // Differences: 
            // - Function (lambda with vexrp) type in parameters and variables, 
            // - Primitive value types (primitive sets), ...
            // - Access to set members which are functions like mySet.MyFunc = {...}

            // Fltenning as our translation strategy
            // - Depth-first strategy by collecting sequences. An intermediate node returns a sequence of operations.
            // - Return principle. Every intermediate node translation has to save its result in some variable ('return' by default)
            // - Every node reads variables and write one variable. Including intermediate nodes.

            // - Every script instruction is an operation while its children are variables. Operations: call (named method), copy/write (assignment), arithmetic ('+' etc.)
            // - Alternative: operation name stored in this node or in the first child node (with 'method' parameter)
            //   - Solution: store as a parameter because in this case we can pass methods by-reference in variables
            //   - Exceptions with special node types: ASSIGNMENT (copy), PROJECTION, ... (if CALL then name in the first parameter)
            //   - Flexible: if Name (of CALL) is empty then search for 'method' parameter
            // - Special children: 'this', 'return' where the result is stored (assigned)
            // - Variables can be either values or references to other variables.
            // - Issue: Variable by-reference is (conceptually) a call, i.e., a call without parameters is access on a variable.
            // - Issue: Value with functions (lambda) are encoded as AstNode with vexpr (alternatively, translated value-ops but we might need to refactor the later during optimization)
            // - Issue: Only context variables contain run-time values while operation children represent either values or code for accessing context variables.


            // Assignment is copy method between variables. The value is returned from an expression: call a method, variable access, arithmetics etc.
            // Assignment can copy a value to a variable. The value has to be encoded: primitive, function, array.
            // Operation: op code (var name, method name, arithmetics, projection etc.), read parameters, write parameters. Input params can be values or variable access. Output is always a variable (or nothing for procedures).
            // Theoretically, all values specified in code could be ASSIGNED to variables in the context and cannot be used in parameters. In this case, only variables can be used in parameters. 
            // Any operation is a processor which reads input values/variables and writes to output variable(s). 
            // Principle: Our task is to choose the type of processor (op node type) and provide parameters: input parameters as values or variables, output parameter as a variable.
            // Challange: implement this principle for each ast node so that it works recursively. 
            // Each node returns one or more operations but how to mix operation nodes and variable/parameter nodes? 
            // What do we do with non-operational nodes: names, params, annotations etc. Do we process them and return Variable (child parameter nodes) or they are processed always from their parent operational node?
            // What do we do with lambda/function nodes (parameters, assignments, return value etc.): treat AstNode for vexpr as a value, translate using vexpr translator and use its result as a value.

            // AstNode is a syntactic representation so it distinguishes various forms operations can be written in. 
            // For example, DOT, PROJECTION, CALL, SET are simply various syntactic constructs for representing one idea: how inputs are read and how outputs are written using some processor (code of operation). 
            // Difficulty: different constructs may have different child structure/conventions
            // Difficulty: reprsenting/translating values (in parameters), 
            // Difficulty: representing/translating variables (in parameters). 
            // Syntactically, variables are not distinguishable from function calls - only semantic distinction during resolution. 
            // Therefore -> should we describe access on variables as calls? No. 
            // Then how to distinguish between function calls which are translated into operations and variable access which are translated into paramters/variable access? 
            // For example, is a parameter 'aaa' a variable or some system procedure?
            // The difference is that variable accesses are translated into parameter node while procedure accesses are transalted into new operation node (followed by a parameter node)
            // 1. approach is to distinguish them syntactically: variables do not have parantheses, while procedures always have paranthese. This works for scripts but bad for vexpr. 
            // 2. approach is that the translator is able to determine the role by trying to resolve the name in the local context of variables (perhaps nested). 
            // 3. approach is that even if a parameter is a variable, we generate an operation of access with return to an intermediate variable (which is guaranteed to be a variable).
            // 4. we switch conception and assume that access to a variable just as methods are normal nodes so we may have nested operations.
            // we may have a return result directly in the node or a makred child node (return node). 
            // Important is only that any node can be 
            // - a variable (no children) and the result value is searched as stored among local variables only (but local variables could theoretically also point to other variables if they store an access command)
            // - a procedure (children are parameters) and the result is generated by a procedure which are resolved using well-known locations (schema etc.)
            // - a value (no children) and the result is directly stored in the node so no access/resolution is needed but the question is whether it is in encoded or run-time format. Parameters should store encoded values while variables a run-time value.
            // Thus any node is assumed to represent some result consumed by its parent node: 
            // - write to a variable. Here the parameter node represents what needs to be written into this operation node representing a variable. It is a setter but the operation node encodes a variable only that has to be changed. Alternatively, output (written) variable could be written as a special parameter but then we get a flat structure which is probably not good.
            // - read from a variable. Here the result represents a read value. The variable either is this operation node or its child. 
            // - procedure. Procedurs consume parameters and return some value that can be consumed. 
            // We understand how reading is done: we always specify some name as an offset relative to the context (either variable name or procedure name).
            // However, we do not want to always store the result in a variable because it is already another operation - write (assignment).
            // It is better to assume that any operation returns some value to its parent. The parent can be a script or another node which knows how to consume it. 
            // In this case, it is not necessary to distinguish between variables and methods we just write READ NAME
            // Then we have to disntuish values. Moreover, in code, they need a special (original) encoding (maybe via its children or via its name). Execution means converting *this* direct represendation into a run-time object without using the context or resolution. 
            // A variable can contain a value or a reference (getter, reader) to another variable or method call. 
            // Thus we must syntactically recongnize values as opposed to relative access, and then represent them as different types of nodes. 
            // Thus we can think about a named getter/reader (variables or procedures), named setters/writers (variables), and named values (literals, values).

            // What we cannot do it now? Because now getter/reader from local vars has a special status of primitive operation which must be recongnized in advance and translated into a dedicated operation.
            // In other words, depending on the decision (getter or method) we get different structure of code. 
            // But we want to have the same structure of code independent of whether it is a getter or method call. 
            // Therefore, all nodes are either named accessors or values (terminal accessor). 
            // An accessor can be getter/reader, setter/writer (=assignment), updater or whaterver, and may have arguments (then it can be treated as a call). An accessor produces a result after its work. 
            // A value is a terminal accessor which encodes its output result in itself without external access. It can be marked as a terminal accessor in addition to readers/writers and whatever. 
            // If the interpreter sees that the node has operation (in code) type VAL then it simply converts its content into a result run-time object. (We can reuse them in context state and then the node simply stores a run-time object which can be read/written.)
            // If the interpreter sees a reader then it resolves the name and uses the child nodes as parameters by storing the result as its output. 
            // A writer means assignment, that is, it takes the whatever another expression returns and writes into a variable (here we can use only variables as name). 


            List<ScriptOp> scriptOps = new List<ScriptOp>();
            string name;
            string type;
            string operation;
            ScriptOp op;
            List<ScriptOp> ops;

            switch (Rule)
            {
                case AstRule.NONE: break; // Nothing to do (or generate NOP instruction)

                case AstRule.SCRIPT: // Sequentially process all statements and merge all generated instructions
                    int stmtCount = Children.Count;
                    for (int s = 0; s < stmtCount; s++)
                    {
                        AstNode stmt = GetChild(s);
                        List<ScriptOp> stmtOps = stmt.TranslateNode();
                        scriptOps.AddRange(stmtOps);
                    }
                    break;

                case AstRule.RETURN:
                    // Equivalent to ASSIGNMENT of whatever generated by the expression to 'return' variable
                    break;

                case AstRule.ALLOC: // Allocate a new variable in the context
                    op = new ScriptOp(ScriptOpType.ALLOC, GetChild(1).Name);

                    ScriptOp param1 = new ScriptOp(ScriptOpType.VALUE, "type");
                    param1.Result = new ContextVariable(VariableClass.VAL, "type", GetChild(0).Name);

                    op.AddChild(param1);

                    scriptOps.Add(op);

                    if (Children.Count > 2)
                    {
                        // TODO: Initialization is equivalent to ASSIGNMENT
                    }

                    break;

                case AstRule.ASSIGNMENT: // Copy whatever is generated by the expression/variable to the specified variable
                    op = new ScriptOp(ScriptOpType.WRITE, GetChild(0).Name);

                    // Initialization of the specified variable
                    List<ScriptOp> initOps = GetChild(1).TranslateNode();
                    op.AddChild(initOps[initOps.Count - 1]); // Assumption: only one operation is generated

                    scriptOps.Add(op);
                    break;

                case AstRule.DOT: // Semantically, the same as CALL. Uses two children: the first describes the first special parameter 'this' and the second is the method (CALL) itself with the rest of parameters.
                    {
                        Debug.Assert(GetChild(1).Rule == AstRule.CALL, "Wrong use: second child of a DOT node has to be CALL.");
                        op = new ScriptOp(ScriptOpType.DOT, GetChild(1).GetChild(0).Name);

                        // Add first special parameter 'this' converted from the first sexpr node. Similar to the result from processing in PARAM node.
                        ScriptOp p1 = new ScriptOp(ScriptOpType.VALUE, "this");

                        ops = GetChild(0).TranslateNode();
                        p1.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated

                        op.AddChild(p1);

                        // Add the rest of (normal) parameters. The same as for CALL but they are children of the second child of DOT node.
                        int argCount = GetChild(1).Children.Count;
                        for (int a = 1; a < argCount; a++)
                        {
                            AstNode param = GetChild(1).GetChild(a);
                            Debug.Assert(param.Rule == AstRule.PARAM, "Wrong use: non-first child of a CALL node has to be PARAM.");

                            ops = param.TranslateNode();
                            op.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated
                        }

                        scriptOps.Add(op);
                        break;
                    }

                case AstRule.CALL: // Some processing with reading input parameters. Access to variables is also possible.
                    {
                        Debug.Assert(GetChild(0).Rule == AstRule.NAME, "Wrong use: first child of a CALL node has to be the method NAME.");
                        op = new ScriptOp(ScriptOpType.CALL, GetChild(0).Name);

                        // Add all arguments if they are values or expressions
                        int argCount = Children.Count;
                        for (int a = 1; a < argCount; a++)
                        {
                            AstNode param = GetChild(a);
                            Debug.Assert(param.Rule == AstRule.PARAM, "Wrong use: non-first child of a CALL node has to be PARAM.");

                            ops = param.TranslateNode();
                            op.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated
                        }

                        scriptOps.Add(op);
                        break;
                    }

                case AstRule.PROJECTION: // The same as DOT but the function can be specified by-value (as a body or lambda) rather than by-name
                    // 1. If function is lambda then extract it and define in the corresponding set
                    // This definition is equivalent to operations executed when a new function is defined for a set (copy the corresponding block as if it were defined by the user)

                    name = GetChild(0).Name;

                    // 2. After that use the new (automatic) name of the function in the projection operation
                    op = new ScriptOp(ScriptOpType.PROJECTION, name);

                    break;

                case AstRule.PARAM: // Parameter returns a VALUE which either stores a literal or accesses context variable/function
                    op = new ScriptOp(ScriptOpType.VALUE, GetChild(0).Name);
                    if (GetChild(1).Rule == AstRule.LITERAL) // Value parameter
                    {
                        op.Result = new ContextVariable(VariableClass.VAL, GetChild(0).Name, GetChild(1).Name);
                    }
                    else // Expression parameter
                    {
                        ops = GetChild(1).TranslateNode();
                        op.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated
                    }

                    scriptOps.Add(op);
                    break;

                case AstRule.SEXPR:
                    AstNode sexpr = GetChild(0);

                    if (sexpr.Rule == AstRule.PRODUCT)
                    {
                        // Operands (greater sets) could theoretically produce nested operations so flatenning might be needed
                    }
                    else if (sexpr.Rule == AstRule.DOT || sexpr.Rule == AstRule.PROJECTION || sexpr.Rule == AstRule.DEPROJECTION)
                    {
                        // First child is a nested exprssion (so it is a nested operation which will generated a sequence of instructions but possibly a concrete argument like variable)
                        // Second argument is a function that is applied to the result (set) of the first child
                    }
                    else if (sexpr.Rule == AstRule.CALL)
                    {
                        // Operands could theoretically produce nested operations so flatenning might be needed
                    }

                    break;
            }

            return scriptOps;
        }

        /// <summary>
        /// Analyze this syntactic node and generate instructions in the value context defining a function.
        /// </summary>
        public ValueContext TranslateFunction()
        {
            return null;
        }

        public AstNode(AstRule rule, string name)
            : this(rule)
        {
            Name = name;
        }

        public AstNode(string name)
            : this(AstRule.NAME)
        {
            Name = name;
        }

        public AstNode(AstRule rule)
            : this()
        {
            Rule = rule;
        }

        public AstNode()
        {
            Rule = AstRule.NONE;
        }

    }

    public enum AstRule
    {
        NONE,

        //
        // Sexpr
        //

        SCRIPT, // List of set statements. A program for set processing.

        STATEMENT, // Statement in a script
        ALLOC, // Allocation/declaration of a new variable (with optional initialization)
        FREE, // Allocation/declaration of a new variable (with optional initialization)

        ASSIGNMENT, // Assignment to an existing variable: "myVar=sexpr;"
        RETURN, // Return. Semantically, it is similar assignment: "return sexpr;"

        SEXPR, // Set-oriented expression. It evaluates to a set by applying operations (and functions) to other sets.
        PRODUCT, // New set is defined as a product of greater sets as well as other members. Syntactically, it is a list of members of various types. 
        PROJECTION, // Operation of applying a function to a set which evaluates to another set
        DEPROJECTION, // Deprojection

        //
        // Vexpr
        //

        VSCOPE, // Scope in vexpr including delimiting the whole function body: { vexpr1; vexpr2; }
        DOT, // 
        TUPLE, // Tuple

        // Unary
        NEG,
        NOT,

        // Arithmetics
        MUL,
        DIV,
        ADD,
        SUB,

        // Logic
        LEQ,
        GEQ,
        GRE,
        LES,

        EQ,
        NEQ,

        AND,
        OR,

        //
        // Common
        //

        CALL, // Function (by-name or by-def) applied to something. Used in both vexpr (applied to one value) and sexpr (applied to all).
        PARAM, // It is a parameter of a call
        MEMBER, // It is a node in a product (set) or tuple definition: "type name=vexpr"

        NAME, // Any identifier like name of a set member, variable, argument, function etc.
        TYPE, // Value type. It is a role of a child node like member of set (product) or tuple.

        LITERAL, // Literal. A single primitive value
    }
}
