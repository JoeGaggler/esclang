namespace EscLang.Lex;

public enum LexemeType
{
	None = 0,

	// Spacing
	Spaces,
	EndOfLine,

	// Symbols
	Colon,
	Comma,
	Period,
	Exclamation,
	SemiColon,
	LessThan,
	GreaterThan,
	Equals,
	Minus,
	Plus,
	Star,
	Slash,
	Caret,
	SingleQuote,
	DoubleQuote,

	// Scopes
	ParenOpen,
	ParenClose,
	BracketOpen,
	BracketClose,
	BraceOpen,
	BraceClose,

	// Binary Operators
	LogicalOr,
	LogicalAnd,

	// Text
	Identifier,
	Number,
	LiteralChar,
	LiteralString,

	// Trivia
	Comment,

	// Note: this is used as a sentinel value for switch statements, keep it last.
	EndOfFile,
}

public record Lexeme(LexemeType Type, String Text, Int32 Position, Int32 Line, Int32 Column)
{
}

public static class LexemeExtensions
{
	public static LexemeType GetTypeOfSymbol(this Char symbol) => symbol switch
	{
		':' => LexemeType.Colon,
		'.' => LexemeType.Period,
		',' => LexemeType.Comma,
		'!' => LexemeType.Exclamation,
		'=' => LexemeType.Equals,
		'-' => LexemeType.Minus,
		'+' => LexemeType.Plus,
		'*' => LexemeType.Star,
		'/' => LexemeType.Slash,
		'^' => LexemeType.Caret,
		'\'' => LexemeType.SingleQuote,
		'"' => LexemeType.DoubleQuote,
		';' => LexemeType.SemiColon,
		'<' => LexemeType.LessThan,
		'>' => LexemeType.GreaterThan,
		'(' => LexemeType.ParenOpen,
		')' => LexemeType.ParenClose,
		'[' => LexemeType.BracketOpen,
		']' => LexemeType.BracketClose,
		'{' => LexemeType.BraceOpen,
		'}' => LexemeType.BraceClose,
		_ => throw new InvalidOperationException("No lexeme type for symbol: " + symbol),
	};
}
