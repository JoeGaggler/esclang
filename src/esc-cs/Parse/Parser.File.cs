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
					var node = Parse_File_Expression(input, ref next);
					if (!node.HasValue) { return new(input[next], Error.Message("invalid print expression"), node.Error); }

					position = next;
					nodes.Add(new PrintNode(node.Value));
					break;
				}
				case LexemeType.Identifier when peek.Text == "if":
				{
					var node = Parse_If(input, ref next);
					if (!node) { return new(input[next], Error.Message("invalid if expression"), node.Error); }
					position = next;
					nodes.Add(node.Value);
					break;
				}
				default:
				{
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue) { return new(peek, Error.Message("failed top level statement"), node.Error); }

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
				case LexemeType.Equals:
				{
					var (peek2, next2) = input.Peek(next);
					if (peek2.Type != LexemeType.Equals) { return new(input[position], Error.NotImplemented("assignment not implemented yet")); }
					next = next2;

					const Int32 priority = (Int32)OperatorPriority.EqualTo;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.EqualTo, Right: result.Value));
					break;
				}
				case LexemeType.Exclamation:
				{
					var (peek2, next2) = input.Peek(next);
					if (peek2.Type != LexemeType.Equals) { return new(input[position], Error.NotImplemented("not(!) not implemented yet")); }
					next = next2;

					const Int32 priority = (Int32)OperatorPriority.EqualTo;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.NotEqualTo, Right: result.Value));
					break;
				}
				case LexemeType.LessThan:
				{
					Int32 priority;
					BinaryOperator binaryOperator;
					var (peek2, next2) = input.Peek(next);
					if (peek2.Type == LexemeType.Equals)
					{
						priority = (Int32)OperatorPriority.LessThanOrEqualTo;
						binaryOperator = BinaryOperator.LessThanOrEqualTo;
						next = next2;
					}
					else
					{
						priority = (Int32)OperatorPriority.LessThan;
						binaryOperator = BinaryOperator.LessThan;
					}

					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: binaryOperator, Right: result.Value));
					break;
				}
				case LexemeType.GreaterThan:
				{
					Int32 priority;
					BinaryOperator binaryOperator;
					var (peek2, next2) = input.Peek(next);
					if (peek2.Type == LexemeType.Equals)
					{
						priority = (Int32)OperatorPriority.MoreThanOrEqualTo;
						binaryOperator = BinaryOperator.MoreThanOrEqualTo;
						next = next2;
					}
					else
					{
						priority = (Int32)OperatorPriority.MoreThan;
						binaryOperator = BinaryOperator.MoreThan;
					}

					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: binaryOperator, Right: result.Value));
					break;
				}
				case LexemeType.Period:
				{
					const Int32 priority = (Int32)OperatorPriority.MemberAccess;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					position = next;
					var result = Parse_File_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid dereference expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.MemberAccess, Right: result.Value));
					break;
				}
				case LexemeType.ParenOpen:
				{
					const Int32 priority = (Int32)OperatorPriority.Call;
					if (min_priority >= priority)
					{
						start = position;
						return leftResult;
					}

					var argumentResult = Parse_ArgumentList(input, ref next);
					if (!argumentResult) { return new(input[position], Error.Message("unable to parse argument list"), argumentResult.Error); }
					var arguments = argumentResult.Value;

					position = next;
					leftResult = new(new CallNode(Target: leftResult.Value, Arguments: arguments));
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
	private static ParseResult<SyntaxNode> Parse_File_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_Shared_Expression_Prefix(input, ref start);

	/// <summary>
	/// Parses a declaration expression
	/// </summary>
	/// <remarks>
	/// Preconditions: after the colon
	/// Postcondition: after the declaration
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
		if (!expr.HasValue)
		{
			// allow braces to start on next line
			var (peek, next) = input.Peek(position);
			if (peek.Type == LexemeType.EndOfLine)
			{
				(peek, next) = input.PeekThroughNewline(position);
				if (peek.Type == LexemeType.BraceOpen)
				{
					position = next - 1; // rewind to the brace
					expr = Parse_File_Expression(input, ref position, (Int32)OperatorPriority.Declaration);
					goto resume;
				}
			}

			return new(input[position], Error.Message($"failed assignment expression for declaration expression"), expr.Error);
		}

	resume:
		start = position;
		var right = expr.Value;
		return new(new DeclarationNode(Left: left, Right: right));
	}
}
