using EscLang.Lex;

namespace EscLang.Parse;
partial class Parser
{
	/// <summary>
	/// Parses an argument list
	/// </summary>
	/// <remarks>
	/// Preconditions: after the open paren
	/// Postcondition: after the close paren
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="List&lt;SyntaxNode&gt;"/> result</returns>
	private static ParseResult<List<SyntaxNode>> Parse_ArgumentList(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;
		var args = new List<SyntaxNode>();

		// check for empty arg list
		var (peek, next) = input.Peek(position);
		if (peek.Type == LexemeType.ParenClose)
		{
			start = next;
			return new(args);
		}

		while (true)
		{
			var argResult = Parse_ArgumentList_Expression(input, ref position);
			if (!argResult) { return new(input[position], Error.Message("failed to parse argument"), argResult.Error); }
			args.Add(argResult.Value);

			(peek, next) = input.Peek(position);
			switch (peek.Type)
			{
				case LexemeType.ParenClose:
				{
					start = next;
					return new(args);
				}
				case LexemeType.Comma:
				{
					position = next;
					break;
				}
				default:
				{
					return new(input[start], Error.UnexpectedToken(peek));
				}
			}
		}
	}

	/// <summary>
	/// Parses an argument list expression
	/// </summary>
	/// <remarks>
	/// Preconditions: at the expression
	/// Postcondition: after the expression, either a comma or the end paren
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<SyntaxNode> Parse_ArgumentList_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, Int32 min_priority = 0)
	{
		var position = start;

		var leftResult = Parse_ArgumentList_Expression_Prefix(input, ref position);
		if (!leftResult.HasValue)
		{
			return new(input[start], Error.Message("invalid expression prefix"), leftResult.Error);
		}

		while (true)
		{
			var (peek, next) = input.Peek(position);
			switch (peek.Type)
			{
				case LexemeType.EndOfFile:
				{
					return new(input[start], Error.Message("end of file before closing paren"));
				}
				case LexemeType.ParenClose:
				{
					start = position;
					return new(leftResult.Value);
				}
				case LexemeType.Comma:
				{
					start = position;
					return new(leftResult.Value);
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
					var result = Parse_ArgumentList_Expression(input, ref position, priority);
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
					var result = Parse_ArgumentList_Expression(input, ref position, priority);
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
					var result = Parse_ArgumentList_Expression(input, ref position, priority);
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
					var result = Parse_ArgumentList_Expression(input, ref position, priority);
					if (!result.HasValue) { return new(input[position], Error.Message($"invalid binary operator expression"), result.Error); }
					leftResult = new(new BinaryOperatorNode(Left: leftResult.Value, Operator: BinaryOperator.Minus, Right: result.Value));
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
					var result = Parse_ArgumentList_Expression(input, ref position, priority);
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
					return new(input[position], Error.UnexpectedToken(peek));
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
	private static ParseResult<SyntaxNode> Parse_ArgumentList_Expression_Prefix(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_Shared_Expression_Prefix(input, ref start);
}
