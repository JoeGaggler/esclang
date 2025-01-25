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
			case LiteralStringNode node: { outputFile.Indent(level); outputFile.WriteLine($"\"{node.Text}\""); break; }
			case LiteralNumberNode node: { outputFile.Indent(level); outputFile.WriteLine($"{node.Text}"); break; }
			case LiteralCharNode node: { outputFile.Indent(level); outputFile.WriteLine($"\"{node.Text}\""); break; }
			case IdentifierNode node: { outputFile.Indent(level); outputFile.WriteLine($"identifier: {node.Text}"); break; }

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

			case LogicalNegationNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("not");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
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
		PrintAnalysisScope(outputFile, analysis.Main, 1);
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

		foreach (var step in scope.Expressions)
		{
			PrintAnalysisTypedExpression(outputFile, step, level + 1);
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
			case FunctionExpression funcExp:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"function: return={funcExp.ReturnType}");
				PrintAnalysisScope(outputFile, funcExp.Scope, v + 1);
				break;
			}
			case MemberExpression { Target: TypedExpression target, MemberName: String methodName }:
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
			case DotnetMemberMethodExpression { Type: { } type, ReturnType: { } returnType, Target: { } target, MethodInfo: { } methodInfo }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"dotnet member call");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"method info: {methodInfo}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"type: {type.FullName}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"return type: {returnType.FullName}");
				outputFile.Indent(v + 1);
				outputFile.WriteLine($"target:");
				PrintAnalysisTypedExpression(outputFile, target, v + 2);
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
			case LogicalNegationExpression logicalNegationExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"not");
				PrintAnalysisTypedExpression(outputFile, logicalNegationExpression.Node, v + 1);
				break;
			}
			case CallExpression callExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"call: type={callExpression.Type} return={callExpression.ReturnType}");
				PrintAnalysisTypedExpression(outputFile, callExpression.Target, v + 1);
				foreach (var arg in callExpression.Args)
				{
					PrintAnalysisTypedExpression(outputFile, arg, v + 1);
				}
				break;
			}
			case ParameterExpression parameterExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"parameter: {parameterExpression.Type}");
				break;
			}
			case IntrinsicFunctionExpression { Name: String name, Type: { } type }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"intrinsic: {name}");
				break;
			}
			case ReturnValueExpression { ReturnValue: { } returnValue, Type: { } returnType }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine("return");
				PrintAnalysisTypedExpression(outputFile, returnValue, v + 1);
				break;
			}
			case DeclarationExpression declarationExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"{(declarationExpression.IsStatic ? "static" : "declare")}: {declarationExpression.Identifier} ({declarationExpression.Type})");
				PrintAnalysisTypedExpression(outputFile, declarationExpression.Value, v + 1);
				break;
			}
			case KeywordExpression { Keyword: String keyword, Type: { } type }:
			{
				outputFile.Indent(v);
				outputFile.WriteLine($"keyword: {keyword} ({type})");
				break;
			}
			case VoidExpression:
			{
				outputFile.Indent(v);
				outputFile.WriteLine("void");
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

	public static void PrintTable(TextWriter outputFile)
	{
		var root = Table.Instance.Root;
		PrintTableSlot(outputFile, 1, 0);
	}

	private static void PrintTableSlot(TextWriter outputFile, int slotId, int level)
	{
		var slot = Table.Instance[slotId];
		switch (slot.DataType)
		{
			case TableSlotType.File:
			{
				var data = (FileSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"file ({GetTypeSlotName(slot.TypeSlot)})");
				PrintTableSlot(outputFile, data.Main, level + 1);
				break;
			}
			case TableSlotType.Braces:
			{
				var data = (BracesSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"braces ({GetTypeSlotName(slot.TypeSlot)})");
				foreach (var line in data.Lines)
				{
					PrintTableSlot(outputFile, line, level + 1);
				}
				break;
			}
			case TableSlotType.Declare:
			{
				var data = (DeclareSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"{(data.IsStatic ? "static" : "declare")} {data.Name} ({GetTypeSlotName(slot.TypeSlot)})");
				if (data.Type != 0)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("type");
					PrintTableSlot(outputFile, data.Type, level + 2);
				}
				if (data.Value != 0)
				{
					outputFile.Indent(level + 1);
					outputFile.WriteLine("value");
					PrintTableSlot(outputFile, data.Value, level + 2);
				}
				break;
			}
			case TableSlotType.Call:
			{
				var data = (CallSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"call ({GetTypeSlotName(slot.TypeSlot)})");
				PrintTableSlot(outputFile, data.Target, level + 1);
				foreach (var arg in data.Args)
				{
					PrintTableSlot(outputFile, arg, level + 1);
				}
				break;
			}
			case TableSlotType.Integer:
			{
				var data = (IntegerSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"integer={data.Value} ({GetTypeSlotName(slot.TypeSlot)})");
				break;
			}
			case TableSlotType.Add:
			{
				var data = (AddOpSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"add ({GetTypeSlotName(slot.TypeSlot)})");
				PrintTableSlot(outputFile, data.Left, level + 1);
				PrintTableSlot(outputFile, data.Right, level + 1);
				break;
			}
			case TableSlotType.Identifier:
			{
				var data = (IdentifierSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"id: name = {data.Name} ({GetTypeSlotName(slot.TypeSlot)})");
				break;
			}
			case TableSlotType.Return:
			{
				var data = (ReturnSlotData)slot.Data;
				outputFile.Indent(level);
				outputFile.WriteLine($"return ({GetTypeSlotName(slot.TypeSlot)})");
				if (data.Value != 0)
				{
					PrintTableSlot(outputFile, data.Value, level + 1);
				}
				break;
			}
			default:
			{
				outputFile.Indent(level);
				outputFile.WriteLine($"unknown {slot.DataType} = {slot.Data}");
				break;
			}
		}
	}

	public static String GetTypeSlotName(int typeSlotId)
	{
		var typeSlot = Table.Instance.Types[typeSlotId];
		return typeSlot switch
		{
			VoidTypeSlot => "void",
			NativeTypeSlot nativeTypeSlot => nativeTypeSlot.Name,
			UnknownTypeSlot => "unknown",
			_ => "null",
		};
	}

	public static void Indent(this TextWriter textWriter, int level)
	{
		textWriter.Write(new String(' ', level * 2));
	}
}
