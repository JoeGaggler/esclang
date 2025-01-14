using EscLang.Analyze;
using EscLang.Lex;
using EscLang.Parse;

namespace EscLang;

public static class Printer
{
	public static void PrintLexeme(TextWriter textWriter, Lex.Lexeme lexeme, int lexemeIndex)
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

		textWriter.WriteLine($"{lexemeIndex:0000}: {lexeme.Position} + {lexeme.Text.Length} -- ({lexeme.Line}, {lexeme.Column}) -- {lexeme.Type} -- {text}");
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
		foreach (var node in file.Lines)
		{
			PrintSyntax(outputFile, node, lexemes, 0);
		}
	}

	public static void PrintSyntax(TextWriter outputFile, SyntaxNode syntaxNode, Span<Lexeme> lexemes, int level = 0)
	{
		switch (syntaxNode)
		{
			case PrintNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"print");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
				break;
			}

			case LiteralStringNode node: { outputFile.Indent(level); outputFile.WriteLine($"\"{node.Text}\""); break; }
			case LiteralNumberNode node: { outputFile.Indent(level); outputFile.WriteLine($"{node.Text}"); break; }
			case LiteralCharNode node: { outputFile.Indent(level); outputFile.WriteLine($"\"{node.Text}\""); break; }
			case IdentifierNode node: { outputFile.Indent(level); outputFile.WriteLine($"identifier: {node.Text}"); break; }

			case BinaryOperatorNode node:
			{
				String op = node.Operator switch
				{
					BinaryOperator.Plus => "add",
					BinaryOperator.Multiply => "multiply",
					BinaryOperator.Minus => "subtract",
					BinaryOperator.Divide => "divide",
					BinaryOperator.EqualTo => "equals",
					BinaryOperator.NotEqualTo => "not equals",
					BinaryOperator.MoreThan => "more than",
					BinaryOperator.LessThan => "less than",
					BinaryOperator.MoreThanOrEqualTo => "more than or equal to",
					BinaryOperator.LessThanOrEqualTo => "less than or equal to",
					BinaryOperator.MemberAccess => "member access",
					_ => "unknown binary operator: " + node.Operator.ToString()
				};
				outputFile.Indent(level);
				outputFile.WriteLine(op);
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case IfNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("if");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("condition");
				PrintSyntax(outputFile, node.Condition, lexemes, level + 2);

				outputFile.Indent(level + 1);
				outputFile.WriteLine("block");
				PrintSyntax(outputFile, node.Block, lexemes, level + 2);

				if (node.ElseBlock is { } elseBlock)
				{
					outputFile.Indent(level);
					outputFile.WriteLine("else");

					outputFile.Indent(level + 1);
					outputFile.WriteLine("block");
					PrintSyntax(outputFile, elseBlock, lexemes, level + 2);
				}

				break;
			}

			case Block node:
			{
				foreach (var statement in node.Statements)
				{
					PrintSyntax(outputFile, statement, lexemes, level);
				}
				break;
			}

			case ParensNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("parens");
				if (node.Node is { } child)
				{
					PrintSyntax(outputFile, child, lexemes, level + 1);
				}
				break;
			}

			case CommaNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("comma");

				foreach (var statement in node.Items)
				{
					PrintSyntax(outputFile, statement, lexemes, level + 1);
				}
				break;
			}

			case FunctionNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("function");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("parameters");
				foreach (var p in node.Parameters)
				{
					PrintSyntax(outputFile, p, lexemes, level + 2);
				}

				// if (node.ReturnType is SyntaxNode returnType)
				// {
				// 	outputFile.Indent(level + 1);
				// 	outputFile.WriteLine("return type");
				// 	PrintSyntax(outputFile, returnType, lexemes, level + 2);
				// }

				outputFile.Indent(level + 1);
				outputFile.WriteLine("body");
				PrintSyntax(outputFile, node.Body, lexemes, level + 2);

				break;
			}

			case BracesNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("braces");

				foreach (var statement in node.Lines)
				{
					PrintSyntax(outputFile, statement, lexemes, level + 1);
				}
				break;
			}

			case ReturnNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("return");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
				break;
			}

			case LogicalNegationNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("not");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
				break;
			}

			case FunctionDeclarationNode node:
			{
				outputFile.Indent(level);
				if (node.ReturnType is { } ret)
				{
					outputFile.WriteLine("function declaration");
					PrintSyntax(outputFile, ret, lexemes, level + 1);
				}
				else
				{
					outputFile.WriteLine("procedure declaration");
					break;
				}
				break;
			}

			////////// TODO: Migrate cases above

			case LineNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("line");
				foreach (var child in node.Items)
				{
					PrintSyntax(outputFile, child, lexemes, level + 1);
				}
				break;
			}

			case DeclareNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("declare-only");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("id");
				PrintSyntax(outputFile, node.Identifier, lexemes, level + 2);

				if (node.Type is { } middle)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("type");
					PrintSyntax(outputFile, middle, lexemes, level + 2);
				}

				break;
			}

			case DeclareStaticNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("declare-static");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("id");
				PrintSyntax(outputFile, node.Identifier, lexemes, level + 2);

				if (node.Type is { } middle)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("type");
					PrintSyntax(outputFile, middle, lexemes, level + 2);
				}

				if (node.Value is { } right)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("value");
					PrintSyntax(outputFile, right, lexemes, level + 2);
				}
				break;
			}

			case DeclareAssignNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("declare-assign");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("id");
				PrintSyntax(outputFile, node.Identifier, lexemes, level + 2);

				if (node.Type is { } middle)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("type");
					PrintSyntax(outputFile, middle, lexemes, level + 2);
				}

				if (node.Value is { } right)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("value");
					PrintSyntax(outputFile, right, lexemes, level + 2);
				}
				break;
			}

			case DotNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("dot");
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case AssignNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("assign");
				PrintSyntax(outputFile, node.Target, lexemes, level + 1);
				PrintSyntax(outputFile, node.Value, lexemes, level + 1);
				break;
			}

			case MemberNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("member");
				PrintSyntax(outputFile, node.Target, lexemes, level + 1);
				PrintSyntax(outputFile, node.Member, lexemes, level + 1);
				break;
			}

			case EmptyNode:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("(empty)");
				break;
			}

			case CallNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("call");

				outputFile.Indent(level + 1);
				outputFile.WriteLine("target");
				PrintSyntax(outputFile, node.Target, lexemes, level + 2);

				if (node.Arguments.Count > 0)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("arguments");
					foreach (var arg in node.Arguments)
					{
						PrintSyntax(outputFile, arg, lexemes, level + 2);
					}
				}
				break;
			}

			case PlusNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("plus");
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case MinusNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("minus");
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case StarNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("star");
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case SlashNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("slash");
				PrintSyntax(outputFile, node.Left, lexemes, level + 1);
				PrintSyntax(outputFile, node.Right, lexemes, level + 1);
				break;
			}

			case ParameterNode:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("parameter");
				break;
			}

			case LeftArrowNode:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("left-arrow");
				break;
			}

			default:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"Unable to print syntax node: {syntaxNode.GetType().Name}");
				break;
			}
		}
	}

	public static void PrintAnalysis(TextWriter outputFile, Analysis analysis)
	{
		outputFile.WriteLine($"main procedure");
		PrintAnalysis(outputFile, analysis.Main, 1);
	}

	public static void PrintAnalysis(TextWriter outputFile, Scope scope, int level)
	{
		outputFile.Indent(level);
		PrintAnalysisScope(outputFile, scope, level);
	}

	public static void PrintAnalysisScope(TextWriter outputFile, Scope scope, int level)
	{
		outputFile.Indent(level);
		outputFile.WriteLine($"scope");
		// foreach (var (key, value) in scope.NameTable)
		// {
		// 	outputFile.Indent(level + 1);
		// 	outputFile.WriteLine($"name: {key} ({value})");
		// }

		foreach (var step in scope.Steps)
		{
			PrintAnalysisStep(outputFile, step, level + 1);
		}
	}

	public static void PrintAnalysisStep(TextWriter outputFile, Step step, int level)
	{
		switch (step)
		{
			case DeclareStep assignStep:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"declare: {assignStep.Identifier} = ({assignStep.Value.GetType().Name})");
				PrintAnalysisTypedExpression(outputFile, assignStep.Value, level + 1);
				break;
			}
			case PrintStep printStep:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"print:");
				PrintAnalysisTypedExpression(outputFile, printStep.Value, level + 1);
				break;
			}
			case ReturnStep returnStep:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"return: {returnStep.Value} ({returnStep.Value.GetType().Name})");
				break;
			}
			case ExpressionStep expressionStep:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"expression");
				PrintAnalysisTypedExpression(outputFile, expressionStep.Value, level + 1);
				break;
			}
			case IfStep ifStep:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"if");
				outputFile.Indent(level + 1);
				outputFile.WriteLine($"condition");
				PrintAnalysisTypedExpression(outputFile, ifStep.Condition, level + 2);
				outputFile.Indent(level + 1);
				outputFile.WriteLine($"block");
				PrintAnalysisTypedExpression(outputFile, ifStep.IfBlock, level + 2);
				break;
			}
			default:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"unknown step: {step.GetType().Name}");
				break;
			}
		}
	}

	private static void PrintAnalysisTypedExpression(TextWriter outputFile, TypedExpression value, int v)
	{
		switch (value)
		{
			case IntLiteralExpression intLiteralExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"int: {intLiteralExpression.Value}");
				break;
			}
			case StringLiteralExpression intLiteralExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"string: {intLiteralExpression.Value}");
				break;
			}
			case BooleanLiteralExpression intLiteralExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"bool: {intLiteralExpression.Value}");
				break;
			}
			case IdentifierExpression identifierExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"identifier: {identifierExpression.Identifier} ({identifierExpression.Type})");
				break;
			}
			case AddExpression addExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"add");
				PrintAnalysisTypedExpression(outputFile, addExpression.Left, v + 1);
				PrintAnalysisTypedExpression(outputFile, addExpression.Right, v + 1);
				break;
			}
			case FunctionScopeExpression funcExp:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"func scope");
				PrintAnalysisScope(outputFile, funcExp.Scope, v + 1);
				break;
			}
			case MemberMethodGroupExpression { Target: TypedExpression target, MethodName: String methodName }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"member method group");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"target");
				PrintAnalysisTypedExpression(outputFile, target, v + 2);
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"method name: {methodName}");
				break;
			}
			case CallExpression { Type: { } type, ReturnType: { } returnType, Target: { } target, Args: { } args, MethodInfo: { } methodInfo }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"call");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"method info: {methodInfo}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"type: {type.FullName}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"return type: {returnType.FullName}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"target:");
				PrintAnalysisTypedExpression(outputFile, target, v + 2);
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"args: {args.Length}");
				foreach (var arg in args)
				{
					PrintAnalysisTypedExpression(outputFile, arg, v + 2);
				}
				break;
			}
			case AssignExpression assignExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"assign");
				PrintAnalysisTypedExpression(outputFile, assignExpression.Target, v + 1);
				PrintAnalysisTypedExpression(outputFile, assignExpression.Value, v + 1);
				break;
			}
			default:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"unknown typed expression: {value.GetType().Name}");
				break;
			}
		}
	}

	public static void Indent(this TextWriter textWriter, int level)
	{
		textWriter.Write(new String(' ', level * 2));
	}
}
