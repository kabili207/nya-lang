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
	| global_attribute
	;

type_delcaration
	: attributes? (class_declaration | interface_declaration)
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

interface_declaration
	: 'interface' identifier ('<<' types)? interface_body
	;

interface_body
	: CurlyLeft interface_member_declarations? CurlyRight
	;

interface_member_declarations
	: interface_member_declaration+
	;

interface_member_declaration
	: (interface_method_declaration)
	;

method_declaration
	: attributes? type_descriptor? identifier Exclamation? RoundLeft fixed_parameters? RoundRight block
	;

interface_method_declaration
	: attributes? type_descriptor? identifier Exclamation? RoundLeft fixed_parameters? RoundRight ';'
	;

attributes
	: attribute+
	;

attribute
	: At (attribute_target ':')? identifier (RoundLeft attribute_arguments? RoundRight)?
	;

global_attribute
	: '@@' (global_attribute_target ':')? identifier (RoundLeft attribute_arguments? RoundRight)?
	;

global_attribute_target
	: 'assembly' | 'module'
	;

attribute_target
	: 'field' | 'event' | 'method' | 'param' | 'property' | 'return' | 'type'
	;

attribute_arguments
	: attribute_argument (','  attribute_argument)*
	;

attribute_argument
	: (identifier ':')? literal
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
	| REGEX_LITERAL        #regexLiteral
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
    | NEW? identifier RoundLeft arguments? RoundRight     #functionExp
	| literal                                        #literalExp
	| expression '.' expression                      #memberExp
    | identifier                                     #nameAtomExp
    ;

identifier
	: IDENTIFIER
	;




TRUE                : 'true' ;
FALSE               : 'false' ;
NULL                : 'nil' ;
NEW                 : 'new' ;

IDENTIFIER:          IdentifierOrKeyword;

LITERAL_ACCESS:      [0-9]+ IntegerTypeSuffix? '.' IdentifierOrKeyword;
INTEGER_LITERAL:     [0-9]+ IntegerTypeSuffix?;
HEX_INTEGER_LITERAL: '0' [xX] HexDigit+ IntegerTypeSuffix?;
REAL_LITERAL:        [0-9]* '.' [0-9]+ ExponentPart? [FfDdMm]? | [0-9]+ ([FfDdMm] | ExponentPart [FfDdMm]?);

/* Original - REGEX_LITERAL:       '/' RegularExpressionChar+ {IsRegexPossible()}? '/' IdentifierPart*; */

REGEX_LITERAL:       '/' RegexContent '/' RegexFlag*;

fragment RegexContent
	: RegexChar+
	;

fragment RegexFlag
	: 'i' | 'm' | 'n' | 's' | 'x'
	;

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


fragment RegexChar
    : ~[\r\n\u2028\u2029\\/[]
    | RegexBackslashSequence
    | '[' RegexClassChar* ']'
    ;
fragment RegexClassChar
    : ~[\r\n\u2028\u2029\]\\]
    | RegexBackslashSequence
    ;
fragment RegexBackslashSequence
    : '\\' ~[\r\n\u2028\u2029]
    ;

Any : . ;