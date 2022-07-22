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
		var nodes = new List<SyntaxNode>();
		while (true)
		{
			var (peek, next) = input.Peek(position);
			switch (peek.Type)
			{
				case LexemeType.EndOfFile:
				{
					return new(input[next], Error.Message("end of file before close paren"));
				}
				case LexemeType.EndOfLine:
				{
					position = next;
					break;
				}
				case LexemeType.ParenClose:
				{
					start = next;
					return new(new ParensNode(nodes));
				}
				case LexemeType.Identifier when peek.Text == "print":
				{
					var node = Parse_Parens_Expression(input, ref next);
					if (!node.HasValue) { return new(input[next], Error.Message("invalid print expression"), node.Error); }

					position = next;
					nodes.Add(new PrintNode(node.Value));
					break;
				}
				default:
				{
					var node = Parse_Parens_Expression(input, ref position);
					if (!node.HasValue) { return new(peek, Error.Message("failed paren level expression"), node.Error); }

					nodes.Add(node.Value);
					break;
				}
			}
		}
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

					var ptr = leftResult.Value;
					if (ptr is CommaTempNode tmp)
					{
						var list = new List<SyntaxNode>();
						void Visit(CommaTempNode temp)
						{
							if (temp.Left is CommaTempNode left)
							{
								Visit(left);
							}
							else
							{
								list.Add(temp.Left);
							}

							if (temp.Right is CommaTempNode right)
							{
								Visit(right);
							}
							else
							{
								list.Add(temp.Right);
							}
						}
						Visit(tmp);
						return new(new CommaNode(Items: list));
					}
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
					var result = Parse_Parens_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid comma expression"), result.Error); }
					leftResult = new(new CommaTempNode(Left: leftResult.Value, Right: result.Value));
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