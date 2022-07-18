namespace EscLang.Parse;

public struct ParseResult<T>
{
	public readonly Boolean HasValue;
	public readonly T Value;
	public readonly ParseError? Error;

	// Implicit cast to boolean to allow use in if-statement
	public static implicit operator bool(ParseResult<T> parseResult) => parseResult.HasValue;

	public ParseResult(T value)
	{
		this.Value = value;
		this.HasValue = true;
		this.Error = default;
	}

	public ParseResult(Int32 position, Int32 errorLine, Int32 errorColumn, String errorMessage, ParseError? previousError)
	{
		this.HasValue = false;
		this.Value = default!;
		this.Error = new(position, errorLine, errorColumn, errorMessage, previousError);
	}

	public ParseResult(EscLang.Lex.Lexeme errorLexeme, String errorMessage) : this(errorLexeme.Position, errorLexeme.Line, errorLexeme.Column, errorMessage, previousError: null)
	{
	}

	public ParseResult(EscLang.Lex.Lexeme errorLexeme, String errorMessage, ParseError? previousError) : this(errorLexeme.Position, errorLexeme.Line, errorLexeme.Column, errorMessage, previousError)
	{
	}
}

public class ParseError
{
	public readonly Int32 Position;
	public readonly Int32 Line;
	public readonly Int32 Column;
	public readonly String ErrorMessage;
	public readonly ParseError? PreviousError;

	public ParseError(Int32 position, Int32 errorLine, Int32 errorColumn, String errorMessage, ParseError? previousError)
	{
		this.Position = position;
		this.Line = errorLine;
		this.Column = errorColumn;
		this.ErrorMessage = errorMessage;
		this.PreviousError = previousError;
	}

	public ParseError(Int32 position, Int32 errorLine, Int32 errorColumn, String errorMessage) : this(position, errorLine, errorColumn, errorMessage, previousError: null)
	{
	}
}
