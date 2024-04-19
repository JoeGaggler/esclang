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

	private static ParseResult<SyntaxNode> Parse_Expression_Leaf(ReadOnlySpan<Lexeme> input, ref Int32 start) => Parse_File_Expression_Prefix(input, ref start);

	private static ParseResult<SyntaxNode> Parse_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, SyntaxNode left, int min_prec = 0)
	{
		var position = start;

		var leftResult = Parse_Expression_Leaf(input, ref position);
		while (true)
		{
			var (peek, next) = input.Peek(position);

			// IsBinaryOperator(peek)
			if (peek.Type switch
			{
				LexemeType.Plus => (int?)BinaryOperator.Plus,
				_ => null
			} is not int prec)
			{
				break;
			}
			if (prec <= min_prec) { break; }

			return new(input[position], Error.NotImplemented($"binary expression"));

			// TODO
			// var right = Parse_Expression(input, ref next, leftResult.Value, prec);
			// leftResult = Combine(leftResult.Value, peek, right.Value);
		}

		start = position;
		return leftResult;
	}
}