grammar Nya;


block_list
	: block*
	;

block
	: assignment SemiColon
	| expression SemiColon
	;

assignment
	: Name Equal expression
	;


assignment_operator
	: '=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
	;

expression
	: RoundLeft expression RoundRight                #parenthesisExp
    | expression (Asterisk|Slash|Percent) expression #mulDivExp
    | expression (Plus|Minus) expression             #addSubExp
    | expression (AngleLeft AngleLeft|AngleRight AngleRight) expression #bitShiftExp
    | expression Ampersand expression                #bitwiseAndExp
    | expression Hat expression                      #bitwiseXorExp
    | expression Pipe expression                     #bitwiseOrExp
    | expression Ampersand Ampersand expression      #logicalAndExp
    | expression Pipe Pipe expression                #logicalOrExp
    | Name RoundLeft expression RoundRight           #functionExp
    | Number                                         #numericAtomExp
    | Name                                           #nameAtomExp
    ;


fragment Letter     : [a-zA-Z] ;
fragment Digit      : [0-9] ;

fragment NewLine
	: '\r\n' | '\r' | '\n'
	| '\u0085'
	| '\u2028'
	| '\u2029'
	;

fragment Whitespace
	: UnicodeClassZS
	| '\u0009'
	| '\u000B'
	| '\u000C'
	;

fragment UnicodeClassZS
	: '\u0020'
	| '\u00A0'
	| '\u1680'
	| '\u180E'
	| '\u2000'
	| '\u2001'
	| '\u2002'
	| '\u2003'
	| '\u2004'
	| '\u2005'
	| '\u2006'
	| '\u2008'
	| '\u2009'
	| '\u200A'
	| '\u202F'
	| '\u3000'
	| '\u205F'
	;


Equal               : '=' ;
Asterisk            : '*' ;
Slash               : '/' ;
Plus                : '+' ;
Minus               : '-' ;
Hat                 : '^' ;
Pipe                : '|' ;
Ampersand           : '&' ;
Percent             : '%' ;
Exclamation         : '!' ;
Question            : '?' ;
At                  : '@' ;
Dollar              : '$' ;
Colon               : ':' ;
SemiColon           : ';' ;
Dot                 : '.' ;
Tilde               : '~' ;
Underscore          : '_' ;
RoundLeft           : '(' ;
RoundRight          : ')' ;
AngleLeft           : '<' ;
AngleRight          : '>' ;
SquareLeft          : '[' ;
SquareRight         : ']' ;
CurlyLeft           : '{' ;
CurlyRight          : '}' ;
QuotationDouble     : '"' ;
QuotationSingle     : '\'' ;


ID                  : Letter Digit ;

Name                : Letter+ ;

Number              : Digit+ ('.' Digit+)? ;

WhiteSpaces          : (Whitespace|NewLine)+ -> skip;

Any : . ;