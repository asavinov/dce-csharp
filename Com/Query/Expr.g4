grammar Expr;
import Common;

init_expr: expr ; // Artificial rule because a rule for expr does not produce a correct method

expr
  : expr (MUL|DIV) expr     # MulDiv // Labels are for visitor pattern - otherwise only one method per rule is generated
  | expr (ADD|SUB) expr     # AddSub
  | INT                     # int
  | ID                      # id
  | '(' expr ')'            # parens
  ;

// Visitor:
// - evaluation
// - Visitor methods must walk their children with explicit visit calls
//   Forgetting to invoke visit() on a node’s children means those subtrees don’t get visited.
// Listener:
// - translation. 
// - Listening to events from a ParseTreeWalker. Listener methods are called by the ANTLR-provided walker object
