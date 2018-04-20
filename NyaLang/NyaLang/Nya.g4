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
	: 'class' identifier ('<<' types)? class_body
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
	: attributes? type_descriptor? identifier Exclamation? RoundLeft fixed_parameters? RoundRight block
	;

attributes
	: attribute+
	;

attribute
	: At identifier
	;

fixed_parameters
	:  fixed_parameter (',' fixed_parameter)*
	;

fixed_parameter
	: type_descriptor identifier (Question | Equal literal)?
	;

type_descriptor
	: attributes? type
	;

types
	: type (',' type)*
	;

type
	: identifier array_type?
	;


type_argument_list
	: '<' type ( ',' type)* '>'
	;

array_type
	: '[]'
	;

block
	: CurlyLeft statement_list* CurlyRight
	;

statement_list
	: statement+
	;

statement
	: assignment ';'       #assignmentStatement
	| embedded_statement   #embeddedStatement
	;

embedded_statement
	: ';'                      #emptyStatement
	| expression ';'           #expressionStatement
	| 'return' expression? ';' #returnStatement
	;

assignment
	: type_descriptor? identifier assignment_operator expression
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

literal
	: boolean_literal      #boolLiteral
	| string_literal       #stringLiteral
	| INTEGER_LITERAL      #integerLiteral
	| HEX_INTEGER_LITERAL  #hexIntLiteral
	| REAL_LITERAL         #realLiteral
	| CHARACTER_LITERAL    #charLiteral
	| NULL                 #nullLiteral
	;

boolean_literal
	: TRUE
	| FALSE
	;

string_literal
	: REGULAR_STRING
	| VERBATIUM_STRING
	;

expression
	: RoundLeft type RoundRight expression           #castExp
	| RoundLeft expression RoundRight                #parenthesisExp
    | expression (Asterisk|Slash|Percent) expression #mulDivExp
    | expression (Plus|Minus) expression             #addSubExp
    | expression (AngleLeft AngleLeft|AngleRight AngleRight) expression #bitShiftExp
    | expression Ampersand expression                #bitwiseAndExp
    | expression Hat expression                      #bitwiseXorExp
    | expression Pipe expression                     #bitwiseOrExp
    | expression Ampersand Ampersand expression      #logicalAndExp
    | expression Pipe Pipe expression                #logicalOrExp
	| expression '??' expression                     #coalesceExp
    | identifier RoundLeft arguments? RoundRight     #functionExp
	| literal                                        #literalExp
    | identifier                                     #nameAtomExp
    ;

identifier
	: IDENTIFIER
	;




TRUE                : 'true' ;
FALSE               : 'false' ;
NULL                : 'nil' ;

IDENTIFIER:          IdentifierOrKeyword;

LITERAL_ACCESS:      [0-9]+ IntegerTypeSuffix? '.' IdentifierOrKeyword;
INTEGER_LITERAL:     [0-9]+ IntegerTypeSuffix?;
HEX_INTEGER_LITERAL: '0' [xX] HexDigit+ IntegerTypeSuffix?;
REAL_LITERAL:        [0-9]* '.' [0-9]+ ExponentPart? [FfDdMm]? | [0-9]+ ([FfDdMm] | ExponentPart [FfDdMm]?);

CHARACTER_LITERAL:                   '\'' (~['\\\r\n\u0085\u2028\u2029] | CommonCharacter) '\'';
REGULAR_STRING:                      '"'  (~["\\\r\n\u0085\u2028\u2029] | CommonCharacter)* '"';
VERBATIUM_STRING:                    '@"' (~'"' | '""')* '"';


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


WhiteSpaces          : (Whitespace|NewLine)+ -> skip;

fragment IntegerTypeSuffix:         [sSlL]? [uU] | [uU]? [sSlL] | [bB] [sS]? | [sS] [bB] ;
fragment ExponentPart:              [eE] ('+' | '-')? [0-9]+;

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

fragment IdentifierOrKeyword
	: IdentifierStartCharacter IdentifierPartCharacter*
	;

fragment IdentifierStartCharacter
	: LetterCharacter
	| '_'
	;

fragment IdentifierPartCharacter
	: LetterCharacter
	| DecimalDigitCharacter
	;

LetterCharacter     : [a-zA-Z] ;
DecimalDigitCharacter : [0-9] ;

fragment UnicodeEscapeSequence
	: '\\u' HexDigit HexDigit HexDigit HexDigit
	| '\\U' HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit
	;

fragment HexDigit : [0-9] | [A-F] | [a-f];

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




Any : . ;