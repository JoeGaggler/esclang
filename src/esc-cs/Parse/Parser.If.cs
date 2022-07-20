using EscLang.Lex;

namespace EscLang.Parse;

partial class Parser
{
	/// <summary>
	/// Parses a if condition expression
	/// </summary>
	/// <remarks>
	/// Preconditions: after the if-keyword
	/// Postcondition: after the if-block
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="IfNode"/> result</returns>
	private static ParseResult<IfNode> Parse_If(ReadOnlySpan<Lexeme> input, ref int start)
	{
		var position = start;

		var conditionResult = Parse_If_Condition_Expression(input, ref position);
		if (!conditionResult) { return new(input[start], Error.Message("invalid if-condition"), conditionResult.Error); }
		var conditionNode = conditionResult.Value;

		var (peek, next) = input.Peek(position);
		switch (peek.Type)
		{
			case LexemeType.BraceOpen:
			{
				break;
			}
			case LexemeType.EndOfLine:
			{
				var (peek2, next2) = input.Peek(next);
				if (peek2.Type != LexemeType.BraceOpen) { return new(input[next], Error.Message("expected open brace on new line after if-condition")); }
				next = next2;
				break;
			}
			default:
			{
				return new(input[start], Error.Message("expected open brace after if-condition"));
			}
		}

		var blockResult = Parse_Block(input, ref next);
		if (!blockResult) { return new(input[position], Error.Message("invalid if-block"), blockResult.Error); }
		var blockNode = blockResult.Value;

		start = next;

		return new(new IfNode(Condition: conditionNode, Block: blockNode));
	}

	/// <summary>
	/// Parses a if condition expression
	/// </summary>
	/// <remarks>
	/// Preconditions: at the expression
	/// Postcondition: after the expression
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_If_Condition_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_If_Condition_Expression_Prefix(input, ref position);
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
				case LexemeType.BraceOpen:
				{
					start = position; // start of the if-block
					return leftResult;
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
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
					var result = Parse_If_Condition_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: binaryOperator, Right: result.Value));
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
	private static ParseResult<SyntaxNode> Parse_If_Condition_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_Shared_Expression_Prefix(input, ref start);
}
