using System.Reflection;
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

			case NegationNode node:
			{
				outputFile.Indent(level);
				outputFile.WriteLine("negation");
				PrintSyntax(outputFile, node.Node, lexemes, level + 1);
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

	public static void PrintTable(Analysis table, TextWriter outputFile)
	{
		PrintTableSlot(table, outputFile, 1, 0);
	}

	private static void PrintTableSlot(Analysis analysis, TextWriter outputFile, int slotId, int level)
	{
		var slot = analysis.GetSlot(slotId);
		switch (slot.CodeType)
		{
			case SlotEnum.Type:
			{
				var data = (TypeSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"type ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.File:
			{
				var data = (FileSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"file ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Main, level + 1);
				break;
			}
			case SlotEnum.Braces:
			{
				var data = (BracesSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"braces ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				foreach (var line in data.Lines)
				{
					PrintTableSlot(analysis, outputFile, line, level + 1);
				}
				break;
			}
			case SlotEnum.Declare:
			{
				var data = (DeclareSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"{(data.IsStatic ? "static" : "declare")} {data.Name} ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				if (data.Type != 0)
				{
					outputFile.WriteIndentLine(level + 1, "type");
					PrintTableSlot(analysis, outputFile, data.Type, level + 2);
				}
				if (data.Value != 0)
				{
					outputFile.WriteIndentLine(level + 1, "value");
					PrintTableSlot(analysis, outputFile, data.Value, level + 2);
				}
				break;
			}
			case SlotEnum.Call:
			{
				var data = (CallSlotData)slot.Data;
				if (data.DotnetMethod is not null)
				{
					outputFile.WriteIndentLine(level, slotId, $"call-dotnet {{ {data.DotnetMethod} }} ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				}
				else
				{
					outputFile.WriteIndentLine(level, slotId, $"call ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				}
				PrintTableSlot(analysis, outputFile, data.Target, level + 1);
				foreach (var arg in data.Args)
				{
					PrintTableSlot(analysis, outputFile, arg, level + 1);
				}
				break;
			}
			case SlotEnum.Integer:
			{
				var data = (IntegerSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"integer={data.Value} ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.Boolean:
			{
				var data = (BooleanSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"boolean={data.Value} ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.String:
			{
				var data = (StringSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"string=\"{data.Value}\" ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.Add:
			{
				var data = (AddSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"add ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Left, level + 1);
				PrintTableSlot(analysis, outputFile, data.Right, level + 1);
				break;
			}
			case SlotEnum.Identifier:
			{
				var data = (IdentifierSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"id: name = {data.Name} ({GetTypeSlotName(analysis, slot.TypeSlot)}){(data.Target != 0 ? $" -> {data.Target:0000}" : "")}");
				break;
			}
			case SlotEnum.Intrinsic:
			{
				var data = (IntrinsicSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"intrinsic: name = {data.Name} ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.Return:
			{
				var data = (ReturnSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"return ({GetTypeSlotName(analysis, slot.TypeSlot)}) -> {data.Function:0000}");
				if (data.Value != 0)
				{
					PrintTableSlot(analysis, outputFile, data.Value, level + 1);
				}
				break;
			}
			case SlotEnum.LogicalNegation:
			{
				var data = (LogicalNegationSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"not ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Value, level + 1);
				break;
			}
			case SlotEnum.Negation:
			{
				var data = (NegationSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"negate ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Value, level + 1);
				break;
			}
			case SlotEnum.Parameter:
			{
				var data = (ParameterSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"parameter ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			case SlotEnum.If:
			{
				var data = (IfSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"if ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Condition, level + 1);
				PrintTableSlot(analysis, outputFile, data.Body, level + 1);
				break;
			}
			case SlotEnum.Assign:
			{
				var data = (AssignSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"assign ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Target, level + 1);
				PrintTableSlot(analysis, outputFile, data.Value, level + 1);
				break;
			}
			case SlotEnum.Member:
			{
				var data = (MemberSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"member ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				PrintTableSlot(analysis, outputFile, data.Target, level + 1);
				PrintTableSlot(analysis, outputFile, data.Member, level + 1);
				break;
			}
			case SlotEnum.Void:
			{
				var data = (VoidSlotData)slot.Data;
				outputFile.WriteIndentLine(level, slotId, $"void ({GetTypeSlotName(analysis, slot.TypeSlot)})");
				break;
			}
			default:
			{
				outputFile.WriteIndentLine(level, slotId, $"unknown {slot.CodeType} = {slot.Data}");
				break;
			}
		}
	}

	public static void PrintTypeTable2(Analysis table, TextWriter outputFile)
	{
		foreach (var (i, xxx) in table.All.Index())
		{
			if (xxx.CodeType != SlotEnum.Type)
			{
				continue;
			}
			outputFile.WriteLine($"{i:0000} {xxx.Parent:0000}: {GetTypeSlotName(table, i)}");
		}
	}

	public static String GetTypeSlotName(Analysis analysis, int typeSlotId2)
	{
		var typeSlot = analysis.GetSlot(typeSlotId2);
		return typeSlot.Data switch
		{
			DotnetMemberTypeSlotData { TargetType: var targetTypeId, MemberName: var memberName, MemberType: var memberType, Members: var members } =>
				$"member -> {memberName} : {memberType switch { MemberTypes.Method => "method", MemberTypes.Property => "property", _ => "unknownof" }} : {GetTypeSlotName(analysis, targetTypeId)}",
			DotnetTypeSlotData { Type: var type } => $"dotnet -> {type.FullName}",
			FuncTypeSlotData { ReturnType: var returnType } => $"func -> {GetTypeSlotName(analysis, returnType)}",
			MetaTypeSlotData { Type: 0 } => $"type",
			MetaTypeSlotData { Type: var type } => $"typeof -> {GetTypeSlotName(analysis, type)}",
			_ when typeSlotId2 == 0 => "unknown",
			_ => $"unknown({typeSlotId2:0000}) -> {typeSlot.Data}",
		};
	}

	public static void Indent(this TextWriter textWriter, int level)
	{
		textWriter.Write(new String(' ', level * 2));
	}

	public static void WriteIndentLine(this TextWriter textWriter, int level, int slot, String text)
	{
		textWriter.Write("{0:0000}: ", slot);
		textWriter.Write(new String(' ', level * 2));
		textWriter.WriteLine(text);
	}

	public static void WriteIndentLine(this TextWriter textWriter, int level, String text)
	{
		textWriter.Write("      ");
		textWriter.Write(new String(' ', level * 2));
		textWriter.WriteLine(text);
	}
}
