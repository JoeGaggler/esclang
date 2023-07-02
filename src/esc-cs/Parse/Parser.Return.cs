using System.Diagnostics.CodeAnalysis;

using EscLang.Lex;

namespace EscLang.Parse;

public static partial class Parser
{
	/// <summary>
	/// Parses a return expression
	/// </summary>
	/// <remarks>
	/// Preconditions: at the expression
	/// Postcondition: after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_Return_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_Parens_Expression_Prefix(input, ref position);
		if (!leftResult.HasValue)
		{
			return new(input[start], Error.Message("invalid expression prefix"), leftResult.Error);
		}

		while (true)
		{
			var (peek, next) = input.Peek(position);
			switch (peek.Type)
			{
				// end of expression tokens
				case LexemeType.EndOfLine:
				{
					start = position; // at EndOfLine
					return leftResult;
				}
				case LexemeType.EndOfFile:
				{
					start = position; // at EndOfFile
					return leftResult;
				}
				
				default:
				{
					return new(peek, Error.UnexpectedToken(peek));
				}
			}
		}
	}
}
