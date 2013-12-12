grammar Expr;
import Common;

expr: expression ; // Artificial rule because expression rule does not produce a correct method

function
  : type name '(' parameter (',' parameter)* ')' '{' statement+ '}'
  ;

parameter // Declaration of parameter, variable, fields and other typed storage elements
  : type name
  ;

statement
  : 'return' expression ';'
  | ';'
  | expression ';'
  ;
  
expression
  : expression (op='.'|op='->'|op='<-') access # AccessPath // Access operator (dot, projection, deprojection etc.)
  | expression (op='*'|op='/') expression      # MulDiv
  | expression (op='+'|op='-') expression      # AddSub
  | expression (op='<=' | op='>=' | op='>' | op='<') expression # Compare
  | expression (op='==' | op='!=') expression  # Equal
  | expression (op='&&') expression            # And
  | expression (op='||') expression            # Or
  | primary                                    # PrimaryRule
  ;

primary
  : literal                                    # LiteralRule // Primitive value
  | access                                     # AccessRule // Start without prefix (variable or function)
  | '(' expression ')'                         # Parens
  ;
  
access
  : name arguments?
  ;

arguments
    :   '(' (expression (',' expression)*)? ')'
    ;

literal // Primitive value
  : DECIMAL
  | INT
  | STRING
  | 'null'
  ;

type : (ID | DELIMITED_ID) ;

name : (ID | DELIMITED_ID) ;

//qualifiedName
//    :   Identifier ('.' Identifier)*
//    ;

// Sample antlr4 grammars: https://github.com/antlr/grammars-v4

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
