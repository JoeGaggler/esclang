using EscLang.Lex;

namespace EscLang.Parse;
partial class Parser
{
	/// <summary>
	/// Parses a file
	/// </summary>
	/// <remarks>
	/// Preconditions: start of a file
	/// Postcondition: end of a file
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="EscFile"/> result</returns>
	private static ParseResult<EscFile> Parse_File(ReadOnlySpan<Lexeme> input, ref Int32 start)
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
					start = next;
					var file = new EscFile(nodes);
					return new(file);
				}
				case LexemeType.EndOfLine:
				{
					position = next;
					break;
				}
				default:
				{
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue)
					{
						return new(peek, "failed top level statement", node.Error);
					}

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
	/// Postcondition: token after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="Expression"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_File_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_File_Expression_Prefix(input, ref position);
		if (!leftResult.HasValue)
		{
			return new(input[start], "invalid expression prefix", leftResult.Error);
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
					return new(peek, Error.UnexpectedToken(nameof(Parse_File_Expression), peek));
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
	private static ParseResult<SyntaxNode> Parse_File_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var token = input.Consume(ref start);
		switch (token.Type)
		{
			case LexemeType.Number: { return new(new LiteralNumberNode(token.Text)); }
			case LexemeType.Identifier:
			{
				switch (token.Text)
				{
					// print is currently an intrinsic keyword
					case "print":
					{
						var leftResult = Parse_File_Expression(input, ref start);
						if (!leftResult.HasValue)
						{
							return new(input[start], "invalid print expression", leftResult.Error);
						}

						return new(new PrintNode(leftResult.Value));
					}
					default:
					{
						return new(new IdentifierNode(token.Text));
					}
				}

			}
			case LexemeType.LiteralString: { return new(new LiteralStringNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.LiteralChar: { return new(new LiteralCharNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.Minus:
			{
				var right = Parse_File_Expression_Prefix(input, ref start);
				if (!right.HasValue)
				{
					return new(token, $"Negation was not followed by a valid expression");
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
			default:
			{
				return new(token, $"unexpected expression atom: {token.Type}");
			}
		}
	}
}
