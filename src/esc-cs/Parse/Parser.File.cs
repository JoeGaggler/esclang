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
				case LexemeType.Identifier when peek.Text == "print":
				{
					position = next;
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue)
					{
						return new(input[start], Error.Message("invalid print expression"), node.Error);
					}

					nodes.Add(new PrintNode(node.Value));
					break;
				}
				default:
				{
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue)
					{
						return new(peek, Error.Message("failed top level statement"), node.Error);
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
	/// Postcondition: after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_File_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_File_Expression_Prefix(input, ref position);
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
				case LexemeType.Colon:
				{
					position = next;
					var result = Parse_File_Expression_Declaration(input, ref position, leftResult.Value);
					if (!result.HasValue) { return new(input[position], Error.Message($"failed file declaration"), result.Error); }
					leftResult = new(result.Value);
					break;
				}
				case LexemeType.Star:
				{
					const Int32 priority = (Int32)OperatorPriority.Multiply;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.Multiply, Right: result.Value));
					break;
				}
				case LexemeType.Slash:
				{
					const Int32 priority = (Int32)OperatorPriority.Divide;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.Divide, Right: result.Value));
					break;
				}
				case LexemeType.Plus:
				{
					const Int32 priority = (Int32)OperatorPriority.Plus;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.Plus, Right: result.Value));
					break;
				}
				case LexemeType.Minus:
				{
					const Int32 priority = (Int32)OperatorPriority.Minus;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.Minus, Right: result.Value));
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
	private static ParseResult<SyntaxNode> Parse_File_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start)
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
				var right = Parse_File_Expression_Prefix(input, ref start);
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
			default:
			{
				return new(token, Error.Message($"unexpected expression atom: {token.Type}"));
			}
		}
	}

	/// <summary>
	/// Parses a declaration expression
	/// </summary>
	/// <remarks>
	/// Preconditions: after the colon
	/// Postcondition: after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_File_Expression_Declaration(ReadOnlySpan<Lexeme> input, ref Int32 start, SyntaxNode left)
	{
		var position = start;

		if (input.ConsumeAny(ref position, LexemeType.Equals, LexemeType.Colon) is not LexemeType mut)
		{
			return new(input[position], Error.NotImplemented("explicit type"));
		}

		// TODO: distiction between :: and := operators

		var expr = Parse_File_Expression(input, ref position, (Int32)OperatorPriority.Declaration);
		if (!expr.HasValue) { return new(input[position], Error.Message($"failed assignment expression for declaration expression"), expr.Error); }

		start = position;
		var right = expr.Value;
		return new(new DeclarationNode(Left: left, Right: right));
	}
}
