grammar Expr;
import Common;

init_expr: expr ; // Artificial rule because a rule for expr does not produce a correct method

expr
  : expr (op=MUL|op=DIV) expr     # MulDiv // Labels are for visitor pattern - otherwise only one method per rule is generated
  | expr (op=ADD|op=SUB) expr     # AddSub
  | INT                     # Int
  | ID                      # Id
  | '(' expr ')'            # Parens
  ;


// Sample antlr4 grammars: https://github.com/antlr/grammars-v4  

/*
TODO:
- we refer to dimensions/functions/fields in this set, other sets, or external sets (in other data source) -> define a rule for a function reference/call
  - it can be a simple identifier like field, say, MyTableColumn, Price
  - It can be a multi-word identifier in delimiters like "List Price". See how MS does it in SQL or else. 
  - It can be a qualified name with Database name, Name space, Schema name, Table space name or whatever. See how it is done in MS. We need only table qualifier and data source qualifier. 
  - ??? Do we distinguish a function returning one value (evaluated, say, for the current row) and a function returning a set of values (say, as a de-projection)?
- we can also refer to variables storing one value (or a set of value depending on type)
  - a variable can be defined in different scopes: data source, set, query. 
  - Examples of variables: this, super (it is a dimension, export/import (these are too dimensions).
  - ??? What is the difference between a variables and a function? A function evaluated for a raw is equivalent to a variable.
    - We can require that variables have different names from functions and also they have simple names (no parameters etc.) So if a variable is met, then it is marked as identifier and then we can decide if it is a variable or a function.
	- Variables can be viewed normal functions which however are not evaluated depending on the domain element. So they are static functions or static fields. 
- We need to define primitive values (actually we could use also complex values - tuples)
  - See how MS defines data types (integers, doubles, strings etc.)
  - We create an expression object for each terminal which has some type (OutputSet). How do we determine types? Grammar has its own data types which can be hard mapped to our types. If expressions can be written for external data sources then we need to use a different grammar (at least different data type section) with mapping to this data source standard.
- We do not have to label each rule by creating a separate visit method. 
  - ??? We can override a generic visit method and then switch and work depending on the node type dynamically. 
  - Process all children (without dedicated visit method) from its parent. For example, Integers and other primitive values do not need to have a dedicated visit method - we can identify primitive and process them from the parent. 
- Access to children:
  - Visit(ctx.expr()) // Paranthese (second child), Assignment 
  - Visit(ctx.expr(1)) // expr can change - it is name of the rule (so possibly it returns a child of this rule type like ctx.ID returns a child of rule ID)
  - Visit(GetChild(1))
  - ctx.INT().getText() // From Integer visitor
  - ctx.ID().getText(); // From Id visitor
  - ctx.op.getType() == LabeledExprParser.MUL // From operation visitor
*/
  
// Visitor:
// - Visitor methods must walk their children with explicit visit calls
// - Visitor supports situations where an application must control *how* a tree is walked
// - Visitor must *explicitly* trigger visits to child nodes to keep the tree traversal going
// - Visitors control the order of traversal and how much of the tree gets visited because of these explicit calls to visit children.
// - Forgetting to invoke visit() on a node’s children means those subtrees don’t get visited.
// - Visitors can return data
// - Visitor does not need a walker - visitor class visits the tree itself
// - evaluation
// - excluding some branches of the tree
// Listener:
// - A listener is an object that responds to rule entry and exit events (phrase recognition events) triggered by a parse-tree walker as it discovers and finishes nodes.
// - Listening to events from a ParseTreeWalker. Listener methods are called by the ANTLR-provided walker object
// - Listeners aren’t responsible for explicitly calling methods to walk their children
// - The listener does not control *how* a tree is walked and cannot influence the direction.
// - Listeners cannot return data. 
// - translation 

// Validating program symbol usage (Section 8.4)
// Correct use of identifiers: variable usages have a visible (in scope) definition; function usages have definitions; variables are not used as functions; functions are not used as variables;
// Data structure: symbol table - a repository of symbol definitions without validation.
// To validate code, we need to check the variable and function references in expressions against the rules we set up earlier.
// There are two fundamental operations for symbol validation: 
// defining symbols and resolving symbols. Defining a symbol means adding it to a scope. 
// Resolving a symbol means figuring out which definition the symbol refers to.


// AST builder. It is a visitor typed by the base node class like ASTNode. So methods will return AST node objects.
// public class ASTBuilder extends FixedFieldBaseVisitor<ASTNode>{
// Visitor methods instantiate and return AST node objects of different types and parameterized:
// ANode a = new ANode();
// NineNode a = new NineNode();
// DSetNode d = new DSetNode(); - data set node
// The question is how to really build the tree by inserting these nodes, generating empty nodes, or more nodes then in parse tree.
// One approach is to use the same strategy as in evaluation: use nodes returned by children to build this node (bottom-up)
// Another strategy is to build the tree in a field of the visitor object by adding the necessary nodes (top-down)

/*
A tree node corresponds to one rule. 
For each tree node (rule) one context object is created. 
A context (base class) has a Parent and children[] with ChildCount.
Also, there are Start and Stop tokens. 
Children represent IParseTree nodes. Parent represents RuleContext. 
Children can be custom contexts (form the grammar) or terminal nodes (TerminalNodeImpl). 
For example, a terminal node can represent an operation (like +) between two non-terminal nodes representing operands. 
Note that this operation terminal node actually corresponds to the parent context representing an operation node (ADD in this case). 

Contexts are typed by the rule, that is, for each rule one context class is created.
For example, AddSubContext has a field IToken::op which stores the type of operation (automatically assigned via the grammar).
Parentheses context has three children: left paranthesis, expression node (say, AddSubContext if it is operation), right paranthesis. 

Example. 
Operation context has three children according to the rule definition: left, op, right. 
The operands can be either other expressions or an integer context. 
An integer context has one child which is a terminal context. 
The operand child is a terminal context. 


Question: given a context of certain type (visited by a method for this rule), we need to learn its child structure which can vary. 
Access to children and visiting (necessary children).
Method Visit(ctx.expr(int)) is used to visit certain children. What is integer here. 

For the children in a rule, dedicated methods are created according to the child names. 
For example, there are two methods ADD and SUB (although they are alternatives).

*/

/*
ASTBuilder builder = new ASTBuilder();
DSetNode exprRoot = (DSetNode) builder.visit(tree); // Returns AST expression root node
System.out.println(exprRoot);


int visitOpExpr(OpExprContext ctx) {

        int left = visit(ctx.left);

        int right = visit(ctx.right);

        switch (ctx.op) {

            case '*' : return left * right;

            case '/' : return left / right;

            case '+' : return left + right;

            case '-' : return left - right;

        }

int visitAtomExpr(AtomExprContext ctx) {

return Integer.parseInt(ctx.atom.getText());
}
*/