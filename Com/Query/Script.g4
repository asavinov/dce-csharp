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
  | ID ID ('=' sexpr)? ';' // Variable allocation
  | ID '=' sexpr ';' // Variable assignment
  | sexpr ';'
  | ';'
  ;

//
// Set/schema expression returns a set and is used only within a script/procedure (not function definition)
//
sexpr
// Access operator. Applying a function to a set expression is also a set expression (returns a new set)
  : sexpr (op='.'|op='->'|op='<-') call
// Set definition. A set is a number of members
  | 'SET' '(' member (',' member)* ')'
// Func definition (assignment?). Adding a function to a set. If it is independent then it is a statement. If it is assignment then it is also a statement (with function type). 
// Set/function population with values (data operations)
// Priority of operations via grouping
//  | '(' sexpr ')'
// Access: set variable or function call
  | call
  ;

//
// Value expression
//
vexpr
// Composition. Access operator.
  : vexpr (op='.') call
// Casting (explicitly specify expression type). It can be used for conversion, for deriving function type etc.
  | '(' type ')' vexpr
  | vexpr (op='*'|op='/') vexpr
  | vexpr (op='+'|op='-') vexpr
  | vexpr (op='<=' | op='>=' | op='>' | op='<') vexpr
  | vexpr (op='==' | op='!=') vexpr
  | vexpr (op='&&') vexpr
  | vexpr (op='||') vexpr
  | literal // Primitive value
  | call // Start without prefix (variable or function)
  | '(' vexpr ')' // Priority
// Tuple (combination)
  | 'TUPLE' '(' member (',' member)* ')'
// Aggregation. Generally, it is not a value op (it cannot be executed) but in source code we can use aggregation-like expressions which have to be compiled into separate (aggregation) functions.
// Global/system/external function call
  ;

//
// Function (body) definition. 
// Together with function signature (input and output types) it evaluates to a function type (function object).
// Function signature can be declared explicitly or derived from the context. 
// Examples of context: set definition where this body is used, expression where this body is used, returned value type declared in the body (for deriving output type) including explicit casting of this expression.
//
vscope
// A single expression like arithmetic, logical (for predicates), tuple (for mapping), composition (dot) etc. 
//  : vexpr // Produces error in grammar
// Full-featured function body consisting of a sequence of value statements
  : '{' (vexpr ';')+ '}'
  ;
  
//
// Access/call request. The procedure can be specified by-reference using ID or by-value using in-place definition (e.g., for projection where it specifies a value-function body)
//
call
  : (name | vscope) ( '(' (param (',' param)*)? ')' )?
  ;

param
  : (name '=')? (vscope | vexpr)
  ;

//
// A member/field of a (complex) value (tuple) or set.
//
member
// Free variable (greater sets, identity dimensions, keys). Special case: Super/Parent, Key (unique, used for varying)
// Bound variable (function). Special cases: Name, Where, Data (non-key and not a function, data loaded explicitly)
  : type name ('=' (vscope | vexpr))?
// Property. We can attach various options and properties as key-value pairs
  ;

name : (ID | DELIMITED_ID) ;

// Value type. But it specifies a concrete set like variable (by reference), set expression, primitive set
type
  : prim_set // Reserved names
  | sexpr
  ;

// Primitive sets. A primitive set is a collection of primitive values (domain).
prim_set
  : 'Double'
  | 'Integer'
  | 'String'
  | 'Root' // Root or Reference or Surrogate
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

// Problems:
// - Typing: How to process primitive types: script types like Schema, Set, Function etc., value types (primitive sets) like String, Double etc.

// - Differences between s-ops (scripts) and v-ops (function definitions): 
//   - scripts have lambdas (function defs with v-ops) as a data type which can be stored in variables and passed in parameters. 
//   - scripts have special data types like Set, Connection, Function etc. while v-ops have only normal primitive data types (although they represent real sets of the schema).
//   - scripts might have special syntactic conventions (in addition to normal calls): PRODUCT, PROJECTION, access to set members like functions mySet.MyFunc = {...}
//   - The use of delimited identifiers might be different (it is not clear yet). In any case, we need to use functions by name and it is convenient also to use sets my their name (rather than variable name)
//   - Common: script can work with values and normal value expressions like string operations and number operations. 
//   - script is never executed in a loop. 
//   - sexpr/vespr issues. The necessity to syntactically distinguish normal script expressions (sexpr like aaa+bbb) from vexpr used for function definitions (lambda, vscope etc.) The latter is not parsed by the script translator and is used as a kind of value for definining objects by assigning some their field.
//   - V-expr could be Java code etc. 
//   - Posdsible syntactic indicators: scope {}, keyword like lambda/func or whatever (means that a function body is provided), parameter type (it actually always is present for parameters but can be reconstructed from various sources: keywords, param name, scope etc. described in this section).
// - Use of delimited identifiers: formally describe where and how. Set names. Function names. Variable names. In scripts and v-expressions.
// - Distinguishing variables from functions: 
//   - Syntactically, variables are not distinguishable from functions
//   1. approach is to distinguish them syntactically: variables do not have parantheses, while procedures always have paranthese. This works for scripts but bad for vexpr. 
//   2. approach is that the translator is able to determine the role by trying to resolve the name in the local context of variables (perhaps nested). 
//   3. approach is that even if a parameter is a variable, we generate an operation of access with return to an intermediate variable (which is guaranteed to be a variable).
//   4. we switch conception and assume that access to a variable just as methods are normal nodes so we may have nested operations.

//
// Translation and scripts
// 

// - Alternative for call/method representation: either in the first child node or in this node (and then children can be only parameters). 
//   - Storing as the first parameter allows us to store a definition for the function easier. Storing in this node means storing a name only. 
//   - Use case: ASSIGMNET (write) store variable name as name
//   - Flexible approach: if Name is empty then search for the first parameter called 'method'
// - Parameter conventions:
//   - 'this' and 'return' (as well as 'method') are a special parameter
//   - parameter is always VALUE which either stores a literal or can contain a CALL or another expression (in non-flat mode). In flat mode, it contains either value or accesses a variable (problem - variable is a call, i.e., expression).
//   - parameters as well as variables can store a function definition, e.g., as ast, as string or as v-ops. So it is a separate value type. 
// - Possibility to have nested execution contexts (but initially use only the parent). 
//   - Creation. For example, a context could be created explicitly from s-scope or implicitly for processing hierarchical nodes which are flattened but we do not want to insert everything in one common root context. 
//   - Use and resolution.
// - Principle: context stores the current state of execution in its variables (possibly in nested contexts) and schema objects.
//   - any operation changes the current state by changing the variables or objects.
//  - parsing or not parsing vexpr-values is another issue. One option is to parse them as usual. Another option is to simply store them as a literal and parse separately.
