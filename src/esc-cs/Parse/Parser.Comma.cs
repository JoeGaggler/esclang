using EscLang.Lex;

namespace EscLang.Parse;
partial class Parser
{
	/// <summary>
	/// Parses a comma expression
	/// </summary>
	/// <remarks>
	/// Preconditions: after a comma
	/// Postcondition: at the comma or other token that indicates the end of a comma separated list
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_Comma_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_Comma_Expression_Prefix(input, ref position);
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
				case LexemeType.ParenClose:
				{
					start = position;
					return leftResult;
				}
				case LexemeType.Comma:
				{
					start = position;
					return leftResult;
				}
				case LexemeType.Colon:
				{
					const Int32 priority = (Int32)OperatorPriority.Colon;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_Parens_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid declaration expression"), result.Error); }
					leftResult = new(new DeclarationNode(Left: leftResult.Value, Right: result.Value));
					break;
				}
				default:
				{
					return new(peek, Error.UnexpectedToken(peek));
				}
			}
		}
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
	private static ParseResult<SyntaxNode> Parse_Comma_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_Shared_Expression_Prefix(input, ref start);
}