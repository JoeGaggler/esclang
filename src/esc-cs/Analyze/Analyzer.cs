namespace EscLang.Analyze;

using System.Reflection;
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
			var step = AnalyzeLineItem(lineItem, scope, queue);
			scope.Steps.Add(step);
		}
	}

	private static Step AnalyzeLineItem(SyntaxNode lineItem, Scope scope, AnalysisQueue queue)
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
			var step = new DeclareStep(scope, Identifier: id, Value: value, IsStatic: true);
			return step;
		}
		else if (lineItem is Parse.DeclareAssignNode declareAssignNode)
		{
			if (declareAssignNode.Identifier is not Parse.IdentifierNode { Text: { Length: > 0 } id })
			{
				throw new Exception("Invalid identifier");
			}

			if (!scope.NameTable.TryAdd(id, null)) // unknown type until right-hand side is analyzed
			{
				throw new Exception("Duplicate identifier");
			}

			var value = AnalyzeExpression(declareAssignNode.Value, scope, queue);
			scope.NameTable[id] = value.Type;
			var step = new DeclareStep(scope, Identifier: id, Value: value, IsStatic: true);
			return step;
		}
		else if (lineItem is AssignNode assignNode)
		{
			var targetResult = AnalyzeExpression(assignNode, scope, queue);
			return new ExpressionStep(scope, Value: targetResult);
		}
		else if (lineItem is CallNode callNode)
		{
			var targetResult = AnalyzeExpression(callNode.Target, scope, queue);
			if (targetResult is KeywordExpression keywordExpression)
			{
				var keyword = keywordExpression.Keyword;
				if (keyword == "return")
				{
					if (callNode.Arguments.Count != 1)
					{
						throw new Exception("Invalid return statement");
					}
					var argumentExpression = AnalyzeExpression(callNode.Arguments[0], scope, queue);
					var step = new ReturnStep(scope, Value: argumentExpression);
					return step;
				}
				if (keyword == "print")
				{
					if (callNode.Arguments.Count != 1)
					{
						throw new Exception("Invalid print call");
					}
					var argumentExpression = AnalyzeExpression(callNode.Arguments[0], scope, queue);
					var step = new PrintStep(scope, Value: argumentExpression);
					return step;
				}
				if (keyword == "if")
				{
					if (callNode.Arguments.Count != 2)
					{
						throw new Exception("Invalid if call");
					}
					var conditionExpression = AnalyzeExpression(callNode.Arguments[0], scope, queue);
					var scopeExpression = AnalyzeExpression(callNode.Arguments[1], scope, queue);
					var step = new IfStep(scope, Condition: conditionExpression, IfBlock: scopeExpression);
					return step;
				}
				throw new NotImplementedException($"TODO CALLNODE KEYWORD: {keyword}");
			}
			throw new NotImplementedException($"TODO CALLNODE RESULT: {targetResult}");
		}
		else if (lineItem is PlusNode)
		{
			// Unused result
			throw new NotImplementedException("Invalid line item: PlusNode");
		}
		else
		{
			throw new NotImplementedException($"Invalid line item: {lineItem}");
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
			case { } x when x is LiteralStringNode { Text: { Length: > 0 } stringLiteral }:
			{
				return new StringLiteralExpression(stringLiteral);
			}
			case { } x when x is IdentifierNode { Text: { Length: > 0 } id }:
			{
				// intrinsic identifiers
				if (id is "return" or "print" or "if")
				{
					return new KeywordExpression(Keyword: id);
				}
				if (id is "true")
				{
					return new BooleanLiteralExpression(Value: true);
				}
				if (id is "false")
				{
					return new BooleanLiteralExpression(Value: false);
				}

				// scoped identifiers
				if (!scope.TryGetNameTableValue(id, out var type))
				{
					throw new Exception($"Unknown identifier: {id}");
				}
				else if (type is null)
				{
					throw new Exception("Unknown identifier type");
				}

				if (type == typeof(FunctionScopeExpression))
				{
					// TODO: analyze return type of function scope
					type = typeof(Int32);
				}

				return new IdentifierExpression(type, Identifier: id);
			}
			case { } x when x is PlusNode { Left: { } left, Right: { } right }:
			{
				var leftValue = AnalyzeExpression(left, scope, queue);
				var rightValue = AnalyzeExpression(right, scope, queue);

				if (leftValue.Type != rightValue.Type)
				{
					throw new Exception($"Type mismatch: left={leftValue.Type}, right={rightValue.Type}");
				}

				var addType = leftValue.Type; // assuming result is same type as operands

				return new AddExpression(addType, Left: leftValue, Right: rightValue);
			}
			case { } x when x is BracesNode { Lines: { } lines }:
			{
				// TODO: all braces are functions for now, future: InlineScopeExpression

				var innerScope = new Scope() { Parent = scope };
				foreach (var line in lines)
				{
					AnalyzeLine(line, innerScope, queue);
				}
				return new FunctionScopeExpression(innerScope);
			}
			case { } x when x is MemberNode { Target: { } target, Member: { } member }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);

				if (member is not IdentifierNode { Text: { Length: > 0 } memberId })
				{
					throw new Exception("Invalid member identifier");
				}

				// Assuming member is method for now
				return new MemberMethodGroupExpression(Target: targetExpression, MethodName: memberId);
			}
			case { } x when x is CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);

				var argumentExpressions = new List<TypedExpression>();
				foreach (var arg in arguments)
				{
					var argValue = AnalyzeExpression(arg, scope, queue);
					argumentExpressions.Add(argValue);
				}

				if (targetExpression is MemberMethodGroupExpression { MethodName: { } methodName, Target: { } methodTarget })
				{
					MethodInfo? found = null;
					foreach (var methodInfo in methodTarget.Type.GetMethods().Where(m => m.Name == methodName))
					{
						// TODO: check argument types
						if (methodInfo.GetParameters().Length != argumentExpressions.Count)
						{
							continue;
						}

						found = methodInfo;
						break;
					}

					if (found is null)
					{
						throw new Exception($"Method not found: {methodName}");
					}

					return new CallDotnetMethodExpression(ReturnType: found.ReturnType, MethodInfo: found, Target: methodTarget, Args: [.. argumentExpressions]);
				}

				if (targetExpression is IdentifierExpression { Identifier: { } identifier, Type: { } type })
				{
					if (!scope.TryGetNameTableValue(identifier, out var targetType))
					{
						throw new Exception($"Unknown identifier: {identifier}");
					}
					if (targetType != typeof(FunctionScopeExpression))
					{
						throw new Exception($"Invalid identifier type: {targetType}");
					}

					var returnType = typeof(Int32); // TODO: analyze return type of function scope

					return new CallExpression(ReturnType: returnType, Target: targetExpression, Args: [.. argumentExpressions]);
				}

				throw new NotImplementedException($"TODO: call node -- target={targetExpression}, arguments={String.Join(", ", argumentExpressions.Select(i => $"{i}"))}");
			}
			case { } x when x is AssignNode { Target: { } target, Value: { } value }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);
				var valueExpression = AnalyzeExpression(value, scope, queue);
				// TODO: TYPE-CHECK
				return new AssignExpression(Type: targetExpression.Type, Target: targetExpression, Value: valueExpression);
			}
			case { } x when x is LogicalNegationNode { Node: { } innerNode }:
			{
				var nodeValue = AnalyzeExpression(innerNode, scope, queue);
				if (nodeValue.Type != typeof(Boolean))
				{
					throw new Exception("Invalid logical negation");
				}
				return new LogicalNegationExpression(nodeValue);
			}
			case { } x when x is ParameterNode:
			{
				return new ParameterExpression();
			}
			default:
			{
				throw new NotImplementedException($"Invalid SyntaxNode for expression: {node}");
			}
		}
	}
}
