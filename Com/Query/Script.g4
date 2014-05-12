grammar Script;
import Common;

//
// Script is a sequence of statements
//
script
  : statement+
  ;

statement
  : 'RETURN' sexpr ';' // Return
  | 'SET' ID ('=' sexpr)? ';' // Variable declaration
  | name '=' sexpr ';' // Set assignment
  | sexpr ';'
  | ';'
  ;

//
// Set/schema expression returns a set and is used only within a script/procedure (not function definition)
//
sexpr
// Access operator. Applying a function to a set expression is also a set expression (returns a new set)
  : sexpr (op='.'|op='->'|op='<-') (name | func_body)
// Set definition. A set is a number of members
  | 'SET' '(' member (',' member)* ')'
// Func definition (assignment?). Adding a function to a set. If it is independent then it is a statement. If it is assignment then it is also a statement (with function type). 
// Set/function population with values (data operations)
// Priority of operations via grouping
//  | '(' sexpr ')' # Parens
// Access: set variable or function call
  | name
  ;

//
// A member of a set.
//
member
// Free variable (greater sets, identity dimensions, keys). Special case: Super/Parent, Key (unique, used for varying)
// Bound variable (function). Special cases: Name, Where, Data (non-key and not a function, data loaded explicitly)
  : type name ('=' func_body)?
// Property. We can attach various options and properties as key-value pairs
  ;

//
// Function (body) definition. 
// Together with function signature (input and output types) it evaluates to a function type (function object).
// Function signature can be declared explicitly or derived from the context. 
// Examples of context: set definition where this body is used, expression where this body is used, returned value type declared in the body (for deriving output type) including explicit casting of this expression.
//
func_body
// A single expression like arithmetic, logical (for predicates), tuple (for mapping), composition (dot) etc. 
  : vexpr
// Full-featured function body consisting of a sequence of value statements
  | '{' (vexpr ';')+ '}'
  ;

//
// Value expression
//
vexpr
// Composition. Access operator.
  : vexpr (op='.') name
// Casting (explicitly specify expression type). It can be used for conversion, for deriving function type etc.
  | '(' type ')' vexpr
  | vexpr (op='*'|op='/') vexpr
  | vexpr (op='+'|op='-') vexpr
  | vexpr (op='<=' | op='>=' | op='>' | op='<') vexpr
  | vexpr (op='==' | op='!=') vexpr
  | vexpr (op='&&') vexpr
  | vexpr (op='||') vexpr
  | literal // Primitive value
  | name // Start without prefix (variable or function)
  | '(' vexpr ')' // Priority, scope
// Tuple (combination)
  | 'TUPLE' '(' name '=' vexpr (',' name '=' vexpr)* ')'
// Aggregation
// Global/system/external function call
  ;

name : (ID | DELIMITED_ID) ;

//
// Value type. But it specifies a concrete set like variable (by reference), set expression, primitive set
//
type
  : primitive_set // Reserved names
  | sexpr
  ;

// Primitive sets. A primitive set is a collection of primitive values (domain).
primitive_set
  : 'Double'
  | 'Integer'
  | 'String'
  | 'Set' // Root or Reference or Surrogate
  | 'Top'
  | 'Bottom'
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
