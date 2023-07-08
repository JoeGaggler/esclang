using EscLang.Lex;

namespace EscLang.Parse;
partial class Parser
{
	/// <summary>
	/// Parses a parentheses node
	/// </summary>
	/// <remarks>
	/// Preconditions: after the open paren
	/// Postcondition: after the close paren
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="ParensNode"/> result</returns>
	private static ParseResult<ParensNode> Parse_Parens(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var (peek, next) = input.PeekThroughNewline(start);
		if (peek.Type == LexemeType.ParenClose)
		{
			start = next;
			return new(new ParensNode());
		}

		var node = Parse_Parens_Expression(input, ref position);
		if (!node.HasValue) { return new(input[position], Error.Message("invalid parens expression"), node.Error); }

		(peek, position) = input.PeekThroughNewline(position);
		if (peek.Type != LexemeType.ParenClose)
		{
			return new(peek, Error.Message("expected close paren"), node.Error);
		}

		start = position;
		return new(new ParensNode(node.Value));
	}

	/// <summary>
	/// Parses a file-level expression
	/// </summary>
	/// <remarks>
	/// Preconditions: at the expression
	/// Postcondition: after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_Parens_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
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
				case LexemeType.ParenClose:
				{
					start = position;
					return new(leftResult.Value);
				}
				case LexemeType.Comma:
				{
					const Int32 priority = (Int32)OperatorPriority.Comma;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var list = new List<SyntaxNode>();
					list.Add(leftResult.Value);
					while (true)
					{
						var result = Parse_Comma_Expression(input, ref position, priority);
						if (!result.HasValue) { return new(input[position], Error.Message($"invalid comma expression"), result.Error); }
						list.Add(result.Value);

						var (peek2, next2) = input.Peek(position);
						if (peek2.Type == LexemeType.Comma)
						{
							position = next2;
							continue;
						}
						break;
					}

					leftResult = new(new CommaNode(list));
					break;
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
	private static ParseResult<SyntaxNode> Parse_Parens_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_Shared_Expression_Prefix(input, ref start);
}