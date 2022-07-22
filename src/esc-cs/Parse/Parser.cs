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

				var (peek, next) = input.Peek(position);
				switch (peek.Type)
				{
					case LexemeType.BraceOpen:
					{
						var braceResult = Parse_Braces(input, ref next);
						if (!braceResult) { return new(input[position], Error.Message("unable to parse braces"), braceResult.Error); }
						start = next;
						return new(new FunctionNode(Parameters: result.Value, ReturnType: null, Body: braceResult.Value));
					}
					case LexemeType.Minus:
					{
						var (peek2, next2) = input.Peek(next);
						if (peek2.Type != LexemeType.GreaterThan)
						{
							start = position;
							return new(result.Value);
						}

						var returnResult = Parse_ReturnType_Expression(input, ref next2);
						if (!returnResult) { return new(input[next], Error.Message("unable to parse return type"), returnResult.Error); }

						(peek2, next2) = input.Peek(next2);
						if (peek2.Type != LexemeType.BraceOpen)
						{
							return new(input[next2], Error.UnexpectedToken(peek2));
						}

						var braceResult = Parse_Braces(input, ref next2);
						if (!braceResult) { return new(input[next2], Error.Message("unable to parse braces"), braceResult.Error); }
						start = next2;
						return new(new FunctionNode(Parameters: result.Value, ReturnType: returnResult.Value, Body: braceResult.Value));
					}
					default:
					{
						start = position;
						return new(result.Value);
					}
				}
			}
			default:
			{
				return new(token, Error.Message($"unexpected expression atom: {token.Type}"));
			}
		}
	}
}