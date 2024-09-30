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
		var nodes = new List<LineNode>();
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

	private static ParseResult<LineNode> Parse_Line(ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var nodes = new List<SyntaxNode>();
		var line = new LineNode(nodes);

		// TODO: check for valid start of line tokens
		var (peek, next) = input.Peek(start);

		// First expression
		var first = Parse_Expression(input, ref start, 0);
		if (!first) { return new(input[start], Error.Message("failed to parse line expression"), first.Error); }
		nodes.Add(first);

		// Trailing expressions
		while (true)
		{
			// Stop if end of file
			(peek, next) = input.Peek(start);
			if (peek.Type is LexemeType.EndOfFile)
			{
				start = next;
				return new(line);
			}
			else if (peek.Type is LexemeType.EndOfLine)
			{
				var (peek2, next2) = input.PeekThroughNewline(start);
				if (peek2.Type is not LexemeType.BraceOpen)
				{
					start = next;
					return new(line);
				}
				throw new NotImplementedException("braces after newline");
				// var right = Parse_Braces2(input, ref start);
				// if (!right) { return new(peek, Error.Message("failed to parse braces expression"), right.Error); }
				// nodes.Add(right);
			}
			else if (peek.Type is LexemeType.Identifier)
			{
				var right = Parse_Expression(input, ref start, 0);
				if (!right) { return new(peek, Error.Message("failed to parse line expression 2"), right.Error); }
				nodes.Add(right);
			}
			else if (peek.Type is LexemeType.BraceOpen)
			{
				start = next;
				var right = Parse_Braces(input, ref start);
				if (!right) { return new(peek, Error.Message("failed to parse line expression 3"), right.Error); }
				nodes.Add(right);
			}
			else if (peek.Type is LexemeType.BraceClose)
			{
				return new(line);
			}
			else
			{
				return new(peek, Error.Message("expected line token"));
			}
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
				return new(new FunctionNode(Parameters: [], ReturnType: null, Body: braceResult.Value));
			}
			case LexemeType.Colon or LexemeType.Equals:
			{
				// This is only valid for a declaration, e.g. "a := 1"
				start = prev;
				return new(new EmptyNode());
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
	private const int prec_dot = 5;

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

				LexemeType.Period => prec_dot,
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
				if (prec == prec_colon)
				{
					// fallthrough, double-colon is right associative
				}
				else
				{
					break;
				}
			}

			position = next;
			var right = peek.Type switch
			{
				LexemeType.Colon => Parse_Declaration(leftResult.Value, input, ref position, prec),
				// LexemeType.Equals => new(new AssignNode(leftResult.Value, right.Value)),
				// LexemeType.Period => new(new DotNode(leftResult.Value, right.Value)),
				_ => throw new NotImplementedException($"binary expression {peek.Type} {position}")
				// _ => Parse_Expression(input, ref position, prec)
			};

			if (!right) { return new(peek, Error.Message("failed to parse right expression"), right.Error); }
			leftResult = right;
		}

		start = position;
		return leftResult;
	}

	private static ParseResult<SyntaxNode> Parse_Declaration(SyntaxNode left, ReadOnlySpan<Lexeme> input, ref Int32 start, int min_prec)
	{
		var position = start;

		var (peek, next) = input.Peek(position);
		if (peek.Type is LexemeType.Colon)
		{
			position = next;
			var rightResult = Parse_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareStaticNode(left, null, rightResult.Value));
		}
		else if (peek.Type is LexemeType.Equals)
		{
			position = next;
			var rightResult = Parse_Expression(input, ref position, min_prec);
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
			return new(new DeclareAssignNode(left, midResult.Value, null)); // defaults to assignment
		}
		else if (peek.Type is LexemeType.Colon)
		{
			position = next;
			var rightResult = Parse_Expression(input, ref position, min_prec);
			if (!rightResult.HasValue) { return new(input[position], "Unable to parse leaf for declaration expression.", rightResult.Error); }
			start = position;
			return new(new DeclareStaticNode(left, midResult.Value, rightResult.Value));
		}
		else if (peek.Type is LexemeType.Equals)
		{
			position = next;
			var rightResult = Parse_Expression(input, ref position, min_prec);
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
		var lines = new List<LineNode>();

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