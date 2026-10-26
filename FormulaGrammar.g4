grammar FormulaGrammar;

compileUnit
    : expr EOF
    ;

expr
    : MAX '(' expr ',' expr ')'                   # MaxExpr
    | MIN '(' expr ',' expr ')'                   # MinExpr
    | '(' expr ')'                                # ParenthesesExpr
    | op=('+'|'-') expr                           # UnaryExpr
    | <assoc=right> left=expr '^' right=expr      # PowerExpr
    | left=expr op=('*'|'/'|MOD|DIV) right=expr   # MultiplicativeExpr
    | left=expr op=('+'|'-') right=expr           # AdditiveExpr
    | NUMBER                                      # NumberExpr
    | IDENTIFIER                                  # CellExpr
    ;

MAX : 'max' | 'MAX' ;
MIN : 'min' | 'MIN' ;
MOD : 'mod' | 'MOD' ;
DIV : 'div' | 'DIV' ;

NUMBER : [0-9]+ ('.' [0-9]+)? ;
IDENTIFIER : [A-Za-z]+ [1-9][0-9]* ;
WS : [ \t\r\n]+ -> skip ;