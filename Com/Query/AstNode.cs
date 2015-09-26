﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Com.Model;
using Com.Utils;

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
        /// Analyze this and all child syntactic nodes and generate a list of script instructions.
        /// Note that AST is flattened during translation (e.g., depth-first strategy) because the output is a sequence while the input is a (syntactic) hierarchy.
        /// Flatenning can be based on extracting expressions (e.g., function definitions), storing their result explicitly in a variable and then reading this variable via a parameter in the next instructions.
        /// </summary>
        public List<ScriptOp> TranslateNode()
        {
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

                case AstRule.PRODUCT: // Create a new set with its functions in one statement
                    // 1. Find set name
                    ScriptOp nameOp = null;
                    foreach (AstNode n in Children)
                    {
                        if (n.GetChild(1).Name != "Name") continue;
                        // TODO: Check correct value type (e.g., it has to be string and cannot be lambda)

                        ops = n.TranslateNode();
                        nameOp = ops[ops.Count - 1]; // Assumption: only one operation is generated
                        break;
                    }
                    if (nameOp == null) // If not specified then warning and automatic name like "My Set 3"
                    {
                        nameOp = new ScriptOp(ScriptOpType.VALUE, "Name");
                        nameOp.Result = new ContextVariable(VariableClass.VAL, "Name", "My Set X");
                    }

                    //
                    // 2. Create a new set object using API call AddSet("MySet")
                    //
                    op = new ScriptOp(ScriptOpType.CALL, "AddSet");
                    op.AddChild(nameOp);

                    // TODO: assign the return value to a newly allocated variable and remember the name of this variable
                    // We do not know the name of the new set, so we will not be able to add functions
                    // Therefore, we have to store a reference to the new set in a intermediate variable which will be then used to create new functions
                    // We need a new assignment operator with value produced by the set creation call above

                    //
                    // 3. In a loop, add functions using AddFunction API call. 
                    //
                    List<ScriptOp> funcOps = new List<ScriptOp>();
                    ScriptOp funcOp;
                    foreach (AstNode n in Children)
                    {
                        // Special members: Name, Super or annotation (super-dim), no definition or annotation like key or id, Where for prodicate or annotation (no body - warning, must be Bool).
                        // Special processing for type (first child in the member) as s-expr (rather than primitive type).
                        Debug.Assert(n.Rule == AstRule.MEMBER, "Wrong use: all children of a PRODUCT node have to be MEMBERs.");
                        Debug.Assert(n.GetChild(1).Rule == AstRule.NAME, "Wrong use: first child of a MEMBER node has to be the member NAME.");
                        if (n.GetChild(1).Name == "Name") continue;

                        funcOp = new ScriptOp(ScriptOpType.CALL, "AddFunction");

                        // Function name
                        ScriptOp funcNameOp = new ScriptOp(ScriptOpType.VALUE, "name");
                        funcNameOp.Result = new ContextVariable(VariableClass.VAL, "name", n.GetChild(1).Name);
                        funcOp.AddChild(funcNameOp);

                        // Input set. The same for all added functions
                        ScriptOp inSetOp = new ScriptOp(ScriptOpType.VALUE, "inputSet");
                        inSetOp.Result = new ContextVariable(VariableClass.VAL, "inputSet", "");
                        funcOp.AddChild(inSetOp);

                        // Output set. 
                        // TODO: In fact, it is a result of expression
                        ScriptOp outSetOp = new ScriptOp(ScriptOpType.VALUE, "outputSet");
                        outSetOp.Result = new ContextVariable(VariableClass.VAL, "outputSet", n.GetChild(0).Name);
                        funcOp.AddChild(outSetOp);

                        // Function formula
                        ScriptOp formulaOp = new ScriptOp(ScriptOpType.VALUE, "formula");
                        formulaOp.Result = new ContextVariable(VariableClass.VAL, "formula", n.GetChild(0).Name);
                        funcOp.AddChild(formulaOp);
                    }

                    break;

                case AstRule.PROJECTION: // The same as DOT but the function can be specified by-value (as a body or lambda) rather than by-name
                    // TODO: Refactor. We want to translate everything to API calls (without COEL-specific statements)
                    // So we need to define a new (possibly intermediate) set from the specification of the function output. 
                    // The popoulation procedure of the set is defined via projection. 
                    // Define a new (temporary) function if the body is provided. 
                    // Ensure that it works if this new set is used for the next projection/de-projection or another operation in an expression.

                    // 1. If function is lambda then extract it and define in the corresponding set
                    // This definition is equivalent to operations executed when a new function is defined for a set (copy the corresponding block as if it were defined by the user)
                    name = GetChild(0).Name;

                    // 2. After that use the new (automatic) name of the function in the projection operation
                    op = new ScriptOp(ScriptOpType.PROJECTION, name);

                    break;

                case AstRule.MEMBER: // Member returns a VALUE (with s-type) which either stores a literal (including lambda) or s-expr (including variables and calls)

                    // TODO: Implement translation of s-type - new sets might have to be created as separate operations. And then these new intermediate sets will be used as types of members.

                    op = new ScriptOp(ScriptOpType.VALUE, GetChild(1).Name);
                    if (Children.Count < 3)
                    {
                        // TODO: Value is not specified - free dimension (key)
                    }
                    if (GetChild(2).Rule == AstRule.LITERAL) // Value parameter
                    {
                        op.Result = new ContextVariable(VariableClass.VAL, GetChild(1).Name, GetChild(2).Name);
                    }
                    else if (GetChild(2).Rule == AstRule.VSCOPE) // Value of type lambda (v-ops for function definition)
                    {
                        op.Result = new ContextVariable(VariableClass.VAL, GetChild(1).Name, GetChild(2));
                    }
                    else // Script expression parameter
                    {
                        ops = GetChild(1).TranslateNode();
                        op.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated
                    }

                    break;

                case AstRule.PARAM: // Parameter returns a VALUE which either stores a literal or s-expr (including variables and calls)
                    op = new ScriptOp(ScriptOpType.VALUE, GetChild(0).Name);
                    if (GetChild(1).Rule == AstRule.LITERAL) // Value parameter
                    {
                        op.Result = new ContextVariable(VariableClass.VAL, GetChild(0).Name, GetChild(1).Name);
                    }
                    else if (GetChild(1).Rule == AstRule.VSCOPE) // Value of type lambda (v-ops for function definition)
                    {
                        op.Result = new ContextVariable(VariableClass.VAL, GetChild(0).Name, GetChild(1));
                    }
                    else // Script expression parameter
                    {
                        ops = GetChild(1).TranslateNode();
                        op.AddChild(ops[ops.Count - 1]); // Assumption: only one operation is generated
                    }

                    scriptOps.Add(op);
                    break;

                case AstRule.SEXPR:
                    AstNode sexpr = GetChild(0);
                    scriptOps.AddRange(sexpr.TranslateNode());
                    break;
            }

            return scriptOps;
        }

        /// <summary>
        /// Analyze this syntactic node and generate instructions in the value context defining a function.
        /// </summary>
        public ValueContext TranslateFormula()
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
