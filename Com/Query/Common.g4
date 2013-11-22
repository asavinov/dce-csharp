lexer grammar CommonLexerRules;

ALPHA : [a-zA-Z] ;
DIGIT : [0-9] ;

// Assign token names. They can be then used as constant in the program
MUL : '*' ;
DIV : '/' ;
ADD : '+' ;
SUB : '-' ;

ID : [a-zA-Z]+ ; // match identifiers -> TODO: We need to define our identification rules: sets, dimensions/functions, data source, ... 
INT : [0-9]+ ; // match integers -> TODO: we need to define double and other literals including strings. We might distinguish between primitive values and complex values (tuples)
NEWLINE:'\r'? '\n' ; // return newlines to parser (is end-statement signal)

COMMENT
  : '/*' .*? '*/' -> skip // channel(HIDDEN) // match anything between /* and */
  ;
WS 
  : [ \t\r\u000C\n]+ -> skip // channel(HIDDEN) // toss out whitespace
  ;
