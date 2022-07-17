using EscLang.Lex;
using EscLang.Parse;

namespace EscLang;

public static class Printer
{
	public static void PrintLexeme(TextWriter textWriter, Lex.Lexeme lexeme)
	{
		String text;
		switch (lexeme.Type)
		{
			case Lex.LexemeType.Spaces:
			{
				text = "\"" + lexeme.Text.Replace("\t", "\\t") + "\"";
				break;
			}
			case Lex.LexemeType.EndOfLine:
			{
				text = lexeme.Text.Replace("\r", "\\r").Replace("\n", "\\n");
				break;
			}
			case Lex.LexemeType.EndOfFile:
			{
				text = "<EOF>";
				break;
			}
			default:
			{
				text = lexeme.Text;
				break;
			}
		}

		textWriter.WriteLine($"{lexeme.Position} + {lexeme.Text.Length} -- ({lexeme.Line}, {lexeme.Column}) -- {lexeme.Type} -- {text}");
	}

	public static String PrintParseError(ReadOnlySpan<Lexeme> input, EscLang.Parse.ParseError error)
	{
		var position = 0;
		for (int i = 0; i < input.Length; i++)
		{
			var j = input[i];
			if (j.Line == error.Line && j.Column == error.Column) // TODO: may not be exact match
			{
				position = i;
				break;
			}
		}
		// TODO: error if end is reached

		var line = error.Line;

		if (input[position].Type == LexemeType.EndOfLine)
		{
			return $"Line {line}: (EndOfLine): {error.ErrorMessage}\r\n";
		}

		if (input[position].Type == LexemeType.EndOfFile)
		{
			return $"Line {line}: (EndOfFile): {error.ErrorMessage}\r\n";
		}

		Int32 left = position - 1;
		while (left > 0)
		{
			if (input[left].Type == LexemeType.EndOfLine) { left++; break; }
			left--;
		}

		Int32 right = position + 1;
		while (right < input.Length - 1)
		{
			if (input[right].Type == LexemeType.EndOfLine) { right--; break; }
			if (input[right].Type == LexemeType.EndOfFile) { right--; break; }
			right++;
		}

		String pre = String.Empty;
		for (Int32 i = left; i >= 0 && i < position; i++)
		{
			pre += input[i].Text;
		}

		String post = String.Empty;
		for (Int32 i = position + 1; i <= right; i++)
		{
			post += input[i].Text;
		}

		String output = $"Parsing failed at ({error.Line},{error.Column}): {error.ErrorMessage}\n";
		output += pre + input[position].Text + post + "\n";
		output += new String(' ', pre.Length);
		output += "↑\n"; //↓

		if (error.PreviousError is var previousError and not null)
		{
			output += "\n\n" + PrintParseError(input, previousError);
		}

		return output;
	}

	public static void PrintSyntax(TextWriter outputFile, EscFile file, Span<Lexeme> lexemes)
	{
		foreach (var node in file.Nodes)
		{
			PrintSyntax(outputFile, node, lexemes, 0);
		}
	}

	public static void PrintSyntax(TextWriter outputFile, SyntaxNode syntaxNode, Span<Lexeme> lexemes, int level = 0)
	{
		outputFile.Indent(level);
		switch (syntaxNode)
		{
			case PrintNode node:
			{
				outputFile.WriteLine($"print");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
				break;
			}

			case LiteralStringNode node: { outputFile.WriteLine($"\"{node.Text}\""); break; }
			case LiteralNumberNode node: { outputFile.WriteLine($"{node.Text}"); break; }
			case LiteralCharNode node: { outputFile.WriteLine($"\"{node.Text}\""); break; }
			case IdentifierNode node: { outputFile.WriteLine($"identifier: {node.Text}"); break; }

			case DeclarationNode node:
			{
				outputFile.WriteLine("declaration");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("left");
				PrintSyntax(outputFile, node.Left, lexemes, level + 2);

				outputFile.Indent(level + 1);
				outputFile.WriteLine("right");
				PrintSyntax(outputFile, node.Right, lexemes, level + 2);
				break;
			}

			case BinaryOperatorNode node:
			{
				String op = node.Operator switch {
					BinaryOperator.Plus => "add",
					BinaryOperator.Multiply => "multiply",
					BinaryOperator.Minus => "subtract",
					BinaryOperator.Divide => "divide",
					_ => "unknown binary operator: " + node.Operator.ToString()
				};
				outputFile.WriteLine(op);
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			default:
			{
				outputFile.WriteLine($"Unable to print syntax node: {syntaxNode.GetType().Name}");
				break;
			}
		}
	}

	public static void Indent(this TextWriter textWriter, int level)
	{
		textWriter.Write(new String(' ', level * 2));
	}
}
