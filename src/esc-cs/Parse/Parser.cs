using System.Diagnostics.CodeAnalysis;
using EscLang.Lex;

namespace EscLang.Parse;

public static partial class Parser
{
	/// <summary>
	/// Tries to parses a file
	/// </summary>
	/// <remarks>
	/// Preconditions: start of a file
	/// Postcondition: end of a file
	/// </remarks>
	/// <param name="input">code</param>
	/// <param name="error">parse error, or <see cref="null"/> if parsing succeeded</param>
	/// <returns><see cref="File"/> result, or <see cref="null"/> if parsing failed</returns>
	public static Boolean TryParse(ReadOnlySpan<Lexeme> lexemes, [NotNullWhen(true)] out EscFile? file, [NotNullWhen(false)] out ParseError? error)
	{
		var start = 0;
		var parsedFile = Parse_File(lexemes, ref start);
		if (!parsedFile.HasValue)
		{
			error = parsedFile.Error!;
			file = null;
			return false;
		}

		error = null;
		file = parsedFile.Value;
		return true;
	}

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
					var node = Parse_Line(input, ref position);
					if (!node.HasValue) { return new(peek, Error.Message("failed top level statement"), node.Error); }

					nodes.Add(node.Value);
					break;
				}
			}
		}
	}

	private static ParseResult<SyntaxNode> Parse_Line(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		// TODO: check for valid start of line tokens
		var (peek, next) = input.Peek(start);

		// First expression
		var first = Parse_Expression(input, ref start, 0);
		if (!first) { return new(input[start], Error.Message("failed to parse line expression"), first.Error); }

		// Line terminator
		(peek, next) = input.Peek(start);
		if (peek.Type is LexemeType.EndOfFile)
		{
			start = next;
			return new(first);
		}
		else if (peek.Type is LexemeType.EndOfLine)
		{
			start = next;
			return new(first);
		}
		else if (peek.Type is LexemeType.BraceClose)
		{
			// caller handles brace close
			return new(first);
		}
		else
		{
			return new(peek, Error.Message("expected line token"));
		}
	}

	private static ParseResult<SyntaxNode> Parse_Leaf(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var prev = start;
		var token = input.Consume(ref start);
		switch (token.Type)
		{
			case LexemeType.Number: { return new(new LiteralNumberNode(token.Text)); }
			case LexemeType.LiteralString: { return new(new LiteralStringNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.LiteralChar: { return new(new LiteralCharNode(EscLang.Lex.Lexer.UnwrapString(token))); }
			case LexemeType.Identifier: { return new(new IdentifierNode(token.Text)); }
			case LexemeType.Exclamation: { return new(new LogicalNegationNode(Parse_Leaf(input, ref start))); }
			case LexemeType.Minus: { return new(new NegationNode(Parse_Leaf(input, ref start))); }
			case LexemeType.ParenOpen:
			{
				var (peek, next) = input.PeekThroughNewline(start);
				if (peek.Type == LexemeType.ParenClose)
				{
					start = next;
					return new(new ParensNode());
				}

				var expr = Parse_Expression(input, ref start, 0);
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
				return new(new BracesNode(braceResult.Value.Lines));
			}
			case LexemeType.LessThan:
			{
				var (peek, next) = input.Peek(start);
				if (peek.Type == LexemeType.GreaterThan)
				{
					start = next;
					return new(new ParameterNode());
				}

				if (peek.Type == LexemeType.Minus)
				{
					start = next;
					return new(new LeftArrowNode());
				}

				return new(input[start], Error.Message("less than not yet implemented"));
			}
			default:
			{
				return new(token, Error.Message($"unexpected expression leaf: {token.Type}"));
			}
		}
	}

	private const int prec_colon = 2;
	private const int prec_equals = 3;
	private const int prec_plus = 4;
	private const int prec_star = 5;
	private const int prec_call = 6;
	private const int prec_dot = 7;

	private static ParseResult<SyntaxNode> Parse_Next_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, int min_prec)
	{
		var position = start;

		var (peek, next) = input.Peek(position);
		while (peek.Type is LexemeType.EndOfLine)
		{
			position = next;
			(peek, next) = input.Peek(position);
		}

		if (peek.Type is LexemeType.EndOfFile)
		{
			return new(input[start], "Unable to parse next expression at end of file.");
		}

		start = position;
		var result = Parse_Expression(input, ref start, min_prec);
		if (!result.HasValue) { return new(peek, "Unable to parse next expression.", result.Error); }
		return result;
	}

	private static ParseResult<SyntaxNode> Parse_Expression(ReadOnlySpan<Lexeme> input, ref Int32 start, int min_prec)
	{
		var position = start;

		var leftResult = Parse_Leaf(input, ref position);
		if (!leftResult.HasValue) { return new(input[start], "Unable to parse leaf for expression.", leftResult.Error); }

		while (true)
		{
			var (peek, next) = input.Peek(position);

			if (peek.Type switch
			{
				LexemeType.Colon => prec_colon,

				LexemeType.Equals => prec_equals,

				LexemeType.Plus => prec_plus,
				LexemeType.Minus => prec_plus,

				LexemeType.Star => prec_star,
				LexemeType.Slash => prec_star,

				LexemeType.Period => prec_dot,

				// contiguous expressions are treated as function calls
				LexemeType.Identifier => prec_call,
				LexemeType.LiteralChar => prec_call,
				LexemeType.LiteralString => prec_call,
				LexemeType.Number => prec_call,
				LexemeType.BraceOpen => prec_call,
				LexemeType.ParenOpen => prec_call,
				LexemeType.Exclamation => prec_call, // logical not (unless a digram?)

				< 0 or > LexemeType.EndOfFile => throw new NotImplementedException($"unexpected token in expression at {position}: {peek.Type}"),

				_ => (int?)null
			} is not int prec)
			{
				// not a binary operator
				break;
			}
			if (prec < min_prec)
			{
				break;
			}
			if (prec == min_prec)
			{
				// break unless operator is right-associative
				if (prec is prec_colon)
				{
					// fallthrough
				}
				else
				{
					// left-associative
					break;
				}
			}

			// HACK: call chain does not have an operator to skip
			if (prec != prec_call)
			{
				position = next;
			}

			var right = peek.Type switch
			{
				LexemeType.Colon => Parse_Declaration(leftResult.Value, input, ref position, prec),
				LexemeType.Plus => Parse_Plus(leftResult.Value, input, ref position),
				LexemeType.Minus => Parse_Minus(leftResult.Value, input, ref position),
				LexemeType.Star => Parse_Star(leftResult.Value, input, ref position),
				LexemeType.Slash => Parse_Slash(leftResult.Value, input, ref position),
				LexemeType.Equals => Parse_Assign(leftResult.Value, input, ref position),
				LexemeType.Period => Parse_Member(leftResult.Value, input, ref position),

				// sequence of expressions without infix operators treated as function calls
				LexemeType.Identifier => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.LiteralChar => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.LiteralString => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.Number => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.BraceOpen => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.ParenOpen => Parse_Call(leftResult.Value, input, ref position),
				LexemeType.Exclamation => Parse_Call(leftResult.Value, input, ref position), // logical not (unless a digram?)

				_ => throw new NotImplementedException($"binary expression {peek.Type} {position}")
				// _ => Parse_Expression(input, ref position, prec)
			};

			if (!right) { return new(peek, Error.Message("failed to parse right expression"), right.Error); }
			leftResult = right;
		}

		start = position;
		return leftResult;
	}

	private static ParseResult<SyntaxNode> Parse_Member(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Next_Expression(input, ref position, prec_dot);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for member expression.", rightResult.Error); }

		start = position;
		return new(new MemberNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Assign(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Next_Expression(input, ref position, prec_equals);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for assign expression.", rightResult.Error); }

		start = position;
		return new(new AssignNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Plus(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Expression(input, ref position, prec_plus);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for plus expression.", rightResult.Error); }

		start = position;
		return new(new PlusNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Minus(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Expression(input, ref position, prec_plus);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for minus expression.", rightResult.Error); }

		start = position;
		return new(new MinusNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Star(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Expression(input, ref position, prec_star);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for star expression.", rightResult.Error); }

		start = position;
		return new(new StarNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Slash(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var position = start;

		var rightResult = Parse_Expression(input, ref position, prec_star);
		if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for slash expression.", rightResult.Error); }

		start = position;
		return new(new SlashNode(left, rightResult.Value));
	}

	private static ParseResult<SyntaxNode> Parse_Call(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		List<SyntaxNode> args = [];

		var position = start;

		while (true)
		{
			var midResult = Parse_Expression(input, ref position, prec_call);
			if (!midResult.HasValue) { break; }
			args.Add(midResult.Value);
		}

		start = position;

		return new(new CallNode(left, args));
	}

	private static ParseResult<SyntaxNode> Parse_Declaration(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start, int min_prec)
	{
		var position = start;

		var (peek, next) = input.Peek(position);
		if (peek.Type is LexemeType.Colon)
		{
			position = next;
			var rightResult = Parse_Next_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareStaticNode(left, null, rightResult.Value));
		}
		else if (peek.Type is LexemeType.Equals)
		{
			position = next;
			var rightResult = Parse_Next_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareAssignNode(left, null, rightResult.Value));
		}

		var midResult = Parse_Expression(input, ref position, prec_equals); // equals is part of the declaration
		if (!midResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", midResult.Error); }

		(peek, next) = input.Peek(position);
		if (peek.Type is LexemeType.EndOfFile or LexemeType.EndOfLine)
		{
			start = position;
			return new(new DeclareNode(left, midResult.Value));
		}
		else if (peek.Type is LexemeType.Colon)
		{
			position = next;
			var rightResult = Parse_Next_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareStaticNode(left, midResult.Value, rightResult.Value));
		}
		else if (peek.Type is LexemeType.Equals)
		{
			position = next;
			var rightResult = Parse_Next_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareAssignNode(left, midResult.Value, rightResult.Value));
		}
		else
		{
			// not implemented
			return new(peek, Error.NotImplemented($"lexeme after declaration type: {peek.Type}"));
		}
	}

	private static ParseResult<BracesNode> Parse_Braces(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var lines = new List<SyntaxNode>();

		while (true)
		{
			var (peek, next) = input.Peek(start);
			if (peek.Type is LexemeType.EndOfLine) { start = next; continue; } // skip empty lines
			var line = Parse_Line(input, ref start);
			if (!line)
			{
				if (peek.Type != LexemeType.BraceClose)
				{
					return new(peek, Error.Message("expected closing brace"), line.Error);
				}
				start = next;
				return new(new BracesNode(lines));
			}
			lines.Add(line);
		}
	}
}
