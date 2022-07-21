using System.Diagnostics.CodeAnalysis;

using EscLang.Lex;

namespace EscLang.Parse;

public static partial class Parser
{
	/// <summary>
	/// Tries to parses a file
	/// </summary>
	/// <remarks>
	/// Preconditions: start of a file
	/// Postcondition: end of a file
	/// </remarks>
	/// <param name="input">code</param>
	/// <param name="error">parse error, or <see cref="null"/> if parsing succeeded</param>
	/// <returns><see cref="File"/> result, or <see cref="null"/> if parsing failed</returns>
	public static Boolean TryParse(ReadOnlySpan<Lexeme> lexemes, [NotNullWhen(true)] out EscFile? file, [NotNullWhen(false)] out ParseError? error)
	{
		var start = 0;
		var parsedFile = Parse_File(lexemes, ref start);
		if (!parsedFile.HasValue)
		{
			error = parsedFile.Error!;
			file = null;
			return false;
		}

		error = null;
		file = parsedFile.Value;
		return true;
	}

	/// <summary>
	/// Parses an expression prefix
	/// </summary>
	/// <remarks>
	/// Preconditions: at the prefix token
	/// Postcondition: after the prefixed expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="Expression"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_Shared_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var token = input.Consume(ref start);
		switch (token.Type)
		{
			case LexemeType.Number: { return new(new LiteralNumberNode(token.Text)); }
			case LexemeType.Identifier: { return new(new IdentifierNode(token.Text)); }
			case LexemeType.LiteralString: { return new(new LiteralStringNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.LiteralChar: { return new(new LiteralCharNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.Minus:
			{
				var right = Parse_Shared_Expression_Prefix(input, ref start);
				if (!right.HasValue)
				{
					return new(token, Error.Message($"Negation was not followed by a valid expression"));
				}
				var inner = right.Value;
				switch (inner)
				{
					// Immediately negate the literal number
					case LiteralNumberNode num:
					{
						return new(new LiteralNumberNode($"-{num.Text}"));
					}

					// Wrap everything else in negation
					default:
					{
						return new(new NegationNode(right.Value));
					}
				}
			}
			case LexemeType.ParenOpen:
			{
				var position = start;
				var result = Parse_Parens(input, ref position);
				if (!result) { return new(input[start], Error.Message("unable to parse parens"), result.Error); }

				start = position;
				return new(new ParensNode(result.Value));
			}
			default:
			{
				return new(token, Error.Message($"unexpected expression atom: {token.Type}"));
			}
		}
	}
}