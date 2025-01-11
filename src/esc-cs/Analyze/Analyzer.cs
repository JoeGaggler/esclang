namespace EscLang.Analyze;

using EscLang.Parse;
using AnalysisQueue = Queue<Object>; // TODO: strong type

public static class Analyzer
{
	public static Analysis Analyze(Parse.EscFile file)
	{
		var queue = new AnalysisQueue();
		var mainScope = new Scope();

		foreach (var line in file.Lines)
		{
			AnalyzeLine(line, mainScope, queue);
		}

		Analysis analysis = new(Main: mainScope);
		return analysis;
	}

	public static void AnalyzeLine(Parse.LineNode line, Scope scope, AnalysisQueue queue)
	{
		foreach (var lineItem in line.Items)
		{
			if (lineItem is Parse.DeclareStaticNode declareStaticNode)
			{
				if (declareStaticNode.Identifier is not Parse.IdentifierNode { Text: { Length: > 0 } id })
				{
					throw new Exception("Invalid identifier");
				}

				if (!scope.NameTable.TryAdd(id, null)) // unknown type until right-hand side is analyzed
				{
					throw new Exception("Duplicate identifier");
				}

				var value = AnalyzeExpression(declareStaticNode.Value, scope, queue);
				scope.NameTable[id] = value.Type;
				var step = new AssignStep(scope, Identifier: id, Value: value);
				scope.Steps.Add(step);
			}
			else if (lineItem is CallNode callNode)
			{
				if (callNode.Target is not IdentifierNode { Text: { Length: > 0 } targetId })
				{
					throw new NotImplementedException("TODO: call nodes on non-identifiers");
				}

				if (targetId == "print") // intrisic
				{
					if (callNode.Arguments.Count != 1)
					{
						throw new Exception("Invalid print call");
					}

					var arg = callNode.Arguments[0];
					var value = AnalyzeExpression(arg, scope, queue);
					var step = new PrintStep(scope, Value: value);
					scope.Steps.Add(step);
				}
				else
				{
					throw new NotImplementedException($"Invalid call target: {targetId}");
				}
			}
			else
			{
				throw new NotImplementedException($"Invalid line item: {lineItem}");
			}
		}
	}

	private static TypedExpression AnalyzeExpression(SyntaxNode? node, Scope scope, AnalysisQueue queue)
	{
		switch (node)
		{
			case { } x when x is LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var intVal = Int32.Parse(numberLiteral);
				return new IntLiteralExpression(intVal);
			}
			case { } x when x is IdentifierNode { Text: { Length: > 0 } id }:
			{
				if (!scope.NameTable.TryGetValue(id, out var type))
				{
					throw new Exception("Unknown identifier");
				}
				else if (type is null)
				{
					throw new Exception("Unknown identifier type");
				}

				return new IdentifierExpression(type, Identifier: id);
			}
			case { } x when x is PlusNode { Left: { } left, Right: { } right }:
			{
				var leftValue = AnalyzeExpression(left, scope, queue);
				var rightValue = AnalyzeExpression(right, scope, queue);

				if (leftValue.Type != rightValue.Type)
				{
					throw new Exception("Type mismatch");
				}

				var addType = leftValue.Type; // assuming result is same type as operands

				return new AddExpression(addType, Left: leftValue, Right: rightValue);
			}
			default:
			{
				throw new NotImplementedException($"Invalid SyntaxNode for expression: {node}");
			}
		}
	}
}
