using EscLang.Lex;

namespace EscLang.Parse;

partial class Parser
{
	/*
	exp parse(min_prec)
	{
		var left = parse_leaf(); // handle prefix operators (ind?)
		while (true)
		{
			var next = peek();
			if (!is_binary_op(next)) break;
			var prec = precedence(next);
			if (prec <= min_prec) break;
			var right = parse(prec);
			left = combine(left, next, right);
		}
	}
	*/

	private static ParseResult<SyntaxNode> Parse_Leaf(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var token = input.Consume(ref start);
		switch (token.Type)
		{
			case LexemeType.Number: { return new(new LiteralNumberNode(token.Text)); }
			case LexemeType.LiteralString: { return new(new LiteralStringNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.LiteralChar: { return new(new LiteralCharNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.Identifier: { return new(new IdentifierNode(token.Text)); }
			case LexemeType.ParenOpen:
			{
				var (peek, next) = input.PeekThroughNewline(start);
				if (peek.Type == LexemeType.ParenClose)
				{
					start = next;
					return new(new ParensNode());
				}

				var expr = Parse_Expression(input, ref start);
				if (!expr.HasValue)
				{
					return new(input[start], Error.Message($"failed to parse expression in parens"), expr.Error);
				}

				(peek, next) = input.PeekThroughNewline(start);
				if (peek.Type != LexemeType.ParenClose)
				{
					return new(peek, Error.Message($"expected close paren after"), expr.Error);
				}

				start = next;
				return expr;
			}
			case LexemeType.BraceOpen:
			{
				var position = start;
				var braceResult = Parse_Braces(input, ref position);
				if (!braceResult) { return new(input[position], Error.Message("unable to parse braces"), braceResult.Error); }
				start = position;
				return new(new FunctionNode(Parameters: [], ReturnType: null, Body: braceResult.Value));
			}
			default:
			{
				return new(token, Error.Message($"unexpected expression leaf: {token.Type}"));
			}
		}
	}

	private static ParseResult<SyntaxNode> Parse_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, int min_prec = 0)
	{
		var position = start;

		var leftResult = Parse_Leaf(input, ref position);
		if (!leftResult.HasValue) { return new(input[start], "Unable to parse leaf for expression.", leftResult.Error); }

		while (true)
		{
			var (peek, next) = input.PeekThroughNewline(position);

			// IsBinaryOperator(peek)
			if (peek.Type switch
			{
				LexemeType.Comma => 1,
				LexemeType.Colon => 2,
				LexemeType.Plus => 3,
				_ => (int?)null
			} is not int prec)
			{
				break;
			}
			if (prec <= min_prec)
			{
				break;
			}

			position = next;
			var right = Parse_Expression(input, ref position, prec);

			leftResult = peek.Type switch
			{
				LexemeType.Colon => new(new DeclarationNode(leftResult.Value, null, right.Value)), // TODO: middle may be embedded in right
				LexemeType.Comma => new(new CommaNode([leftResult.Value, right.Value])),
				_ => throw new NotImplementedException($"binary expression")
			};
		}

		start = position;
		return leftResult;
	}
}