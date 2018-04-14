grammar Nya;

compilation_unit
	: unit_declarations
	;

unit_declarations
	: unit_declaration+
	;

unit_declaration
	: type_delcaration
	| method_declaration
	;

type_delcaration
	: attributes? (class_declaration)
	;

class_declaration
	: 'class' Identifier ('<<' types)? class_body
	;

class_body
	: CurlyLeft class_member_declarations? CurlyRight
	;

class_member_declarations
	: class_member_declaration+
	;

class_member_declaration
	: (method_declaration)
	;

method_declaration
	: attributes? type_descriptor? Identifier Exclamation? RoundLeft fixed_parameters? RoundRight CurlyLeft block CurlyRight
	;

attributes
	: attribute+
	;

attribute
	: At Identifier
	;

fixed_parameters
	:  fixed_parameter (',' fixed_parameter)*
	;

fixed_parameter
	: type_descriptor Identifier Question?
	;

type_descriptor
	: attributes? type
	;

types
	: type (',' type)*
	;

type
	: Identifier array_type?
	;

array_type
	: '[]'
	;

block
	: expression_list*
	;

expression_list
	: assignment SemiColon           #assignmentExp
	| expression SemiColon           #expressExp
	| 'return' expression? SemiColon #returnExp
	;

assignment
	: type_descriptor? Identifier assignment_operator expression
	;


assignment_operator
	: '=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=' | '?='
	;

arguments
	: argument (',' argument)*
	;

argument
	: expression
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
	| expression '??' expression                     #coalesceExp
    | Identifier RoundLeft arguments? RoundRight     #functionExp
	| RoundLeft type RoundRight expression           #castExp
	| REGULAR_STRING                                 #stringExp
    | Number                                         #numericAtomExp
    | Identifier                                     #nameAtomExp
    ;


REGULAR_STRING:     '"'  (~["\\\r\n\u0085\u2028\u2029] | CommonCharacter)* '"';

fragment CommonCharacter
	: SimpleEscapeSequence
	| HexEscapeSequence
	| UnicodeEscapeSequence
	;
fragment SimpleEscapeSequence
	: '\\\''
	| '\\"'
	| '\\\\'
	| '\\0'
	| '\\a'
	| '\\b'
	| '\\f'
	| '\\n'
	| '\\r'
	| '\\t'
	| '\\v'
	;
fragment HexEscapeSequence
	: '\\x' HexDigit
	| '\\x' HexDigit HexDigit
	| '\\x' HexDigit HexDigit HexDigit
	| '\\x' HexDigit HexDigit HexDigit HexDigit
	;

fragment UnicodeEscapeSequence
	: '\\u' HexDigit HexDigit HexDigit HexDigit
	| '\\U' HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit
	;

fragment HexDigit : [0-9] | [A-F] | [a-f];

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


Identifier          : (Letter|Underscore) (Letter|Underscore|Digit)* ;

Name                : Letter+ ;

Number              : Digit+ ('.' Digit+)? ;

WhiteSpaces          : (Whitespace|NewLine)+ -> skip;

Any : . ;