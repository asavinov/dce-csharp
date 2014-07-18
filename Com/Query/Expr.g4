grammar Expr;
import Common;

/*
Task: unify function arguments with tuple members
Task: unify (rule) for <type name = value> fragment
*/

//
// Function (body) definition. 
// Together with function signature (input and output types) it evaluates to a function type (function object).
// Function signature can be declared explicitly or derived from the context. 
// Examples of context: set definition where this body is used, expression where this body is used, returned value type declared in the body (for deriving output type) including explicit casting of this expression.
//
scope
// A single expression like arithmetic, logical (for predicates), tuple (for mapping), composition (dot) etc. 
//  : expr // Produces error in grammar
// Full-featured function body consisting of a sequence of value statements
  : '{' (expr ';')+ '}'
  ;

expr
// Composition. Access operator.
  : expr (op='.') access
// Casting (explicitly specify expression type). It can be used for conversion, for deriving function type etc.
  | '(' type ')' expr
  | expr (op='*'|op='/') expr
  | expr (op='+'|op='-') expr
  | expr (op='<=' | op='>=' | op='>' | op='<') expr
  | expr (op='==' | op='!=') expr
  | expr (op='&&') expr
  | expr (op='||') expr
  | literal // Primitive value
  | access // Start without prefix (variable or function)
// Global/system/external function call
  | '(' expr ')' // Priority
// Tuple (combination)
  | '((' member (',' member)* '))'
// Aggregation. Generally, it is not a value op (it cannot be executed) but in source code we can use aggregation-like expressions which have to be compiled out into separate specialized (aggregation) formula of a separate function.
  ;

//
// Access/call request. The procedure can be specified by-reference using ID or by-value using in-place definition (e.g., for projection where it specifies a value-function body)
//
access
  : (name | scope) ( '(' (param (',' param)*)? ')' )?
  ;

param
  : (name '=')? (scope | expr)
  ;

//
// A member/field of a (complex) value (tuple) or set.
//
member
// Uses:
// Free variable in SET
// Bound variable (function). Special cases: Name, Where, Data (non-key and not a function, data loaded explicitly)
// Tuple member with assigned value (can be another tuple)
// Argument 
// Property, for example, is a key-value pair
// Annotations: Super/Parent, Id/Key (by-value, unique), Free (used for varying, normally determined from the presence of assignment)
  : type name ('=' (scope | expr))?
  ;

name : (ID | DELIMITED_ID) ;

// Value type. But it specifies a concrete set like variable (by reference), set expression, primitive set
type
  : prim_set // Reserved names
  | (ID | DELIMITED_ID)
  ;

// Primitive sets. A primitive set is a collection of primitive values (domain).
prim_set
  : 'Top'
  | 'Bottom'
  | 'Void' // No value is returned or stored. Could be equivalent to Top.
  | 'Root' // Root or Reference or Surrogate
  | 'Integer'
  | 'Double'
  | 'Decimal'
  | 'String'
  | 'Boolean'
  | 'DateTime'
  ;

//
// Primitive value. Concrete instance of some primitive set.
//
literal
  : DECIMAL
  | INT
  | STRING
  | 'null'
  ;

//qualifiedName
//    :   Identifier ('.' Identifier)*
//    ;

//
// Tuple general syntax <Type Name = expr> for expressions
//
// Using tuples a a full syntax in expressions.
// Tuple operator (either TUPLE(...), ((...)) or simply (...)) is a normal operator that can be met in expressions where we expect a value.
// The difference is only in type: normal expressions return primitive values while a tuple returns a combination which belongs to some set, and this type (set) has to be somehow determined.
// It is important to understand that two definitions are equivalent:
// Short: "(c11+c13) * 10.0" Full: "Double f = (c11+c13) * 10.0"
// Short: "(T1 n1=..., T2 n2=...)" Full: "MySet f = (T1 n1=..., T2 n2=...)"

// One solution is to introduce syntax for primitive expressions, where each part (subexpression) may have Type and Name information
// Example: 2.0 -> Double myName=2.0
// Example: a*(c+f1.f2/2.0))

// Sample antlr4 grammars: https://github.com/antlr/grammars-v4

//
// Visitor:
//
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
// To validate code, we need to check the variable and function references in exprs against the rules we set up earlier.
// There are two fundamental operations for symbol validation: 
// defining symbols and resolving symbols. Defining a symbol means adding it to a scope. 
// Resolving a symbol means figuring out which definition the symbol refers to.
