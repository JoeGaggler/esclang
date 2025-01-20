namespace EscLang.Analyze;

using System.Reflection;
using EscLang.Parse;
using AnalysisQueue = Queue<Object>; // TODO: strong type

// TODO: constant enforcement
// TODO: collect declarations before analyzing possible references
// TODO: combine call node handling across step/non-step methods

public static class Analyzer
{
	public static Analysis Analyze(Parse.EscFile file)
	{
		var queue = new AnalysisQueue();
		var globalScope = new Scope();

		var mainFunc = AnalyzeScope(file.Lines, globalScope, queue);

		Analysis analysis = new(Main: mainFunc.Scope);
		return analysis;
	}

	// TODO: all braces are functions for now, future: InlineScopeExpression
	private static FunctionExpression AnalyzeScope(List<SyntaxNode> nodes, Scope parentScope, AnalysisQueue queue)
	{
		// TODO: collect declarations
		// TODO: analyze return type

		var innerScope = new Scope() { Parent = parentScope };

		foreach (var node in nodes)
		{
			var targetResult = AnalyzeExpression(node, innerScope, queue);
			if (targetResult is KeywordExpression { Keyword: "return" })
			{
				innerScope.Expressions.Add(new ReturnVoidExpression());
			}
			else
			{
				innerScope.Expressions.Add(targetResult);
			}
		}

		// TODO: analyze return type of function scope
		var returnType = new DotnetAnalysisType(typeof(int));
		return new FunctionExpression(innerScope, returnType);
	}

	private static AnalysisType? AnalyzeTypeExpression(SyntaxNode? node, Scope scope, AnalysisQueue queue)
	{
		if (node is null) { return null; }
		if (node is IdentifierNode { Text: { } id })
		{
			if (id == "bool") { return new DotnetAnalysisType(typeof(Boolean)); }
			if (id == "int") { return new DotnetAnalysisType(typeof(Int32)); }
			if (id == "string") { return new DotnetAnalysisType(typeof(String)); }
		}
		return null;
	}

	private static TypedExpression AnalyzeExpression(SyntaxNode? node, Scope scope, AnalysisQueue queue)
	{
		switch (node)
		{
			case LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var intVal = Int32.Parse(numberLiteral);
				return new IntLiteralExpression(intVal);
			}
			case LiteralStringNode { Text: { Length: > 0 } stringLiteral }:
			{
				return new StringLiteralExpression(stringLiteral);
			}
			case IdentifierNode { Text: { Length: > 0 } id }:
			{
				// intrinsic identifiers
				if (id is "return" or "print" or "if" or "bool" or "int" or "string")
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

				if (type is FunctionAnalysisType { ReturnType: { } returnType })
				{
					type = returnType;
				}

				return new IdentifierExpression(type, Identifier: id);
			}
			case PlusNode { Left: { } left, Right: { } right }:
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
			case BracesNode { Lines: { } lines }:
			{
				return AnalyzeScope(lines, scope, queue);
			}
			case MemberNode { Target: { } target, Member: { } member }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);

				if (member is not IdentifierNode { Text: { Length: > 0 } memberId })
				{
					throw new Exception("Invalid member identifier");
				}

				// TODO: Assuming member is method for now
				return new MemberMethodGroupExpression(Target: targetExpression, MethodName: memberId);
			}
			case CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);

				var argumentExpressions = new List<TypedExpression>();
				foreach (var arg in arguments)
				{
					var argValue = AnalyzeExpression(arg, scope, queue);
					argumentExpressions.Add(argValue);
				}

				if (targetExpression is KeywordExpression keywordExpression)
				{
					var keyword = keywordExpression.Keyword;
					if (keyword == "return")
					{
						if (argumentExpressions.Count != 1)
						{
							throw new Exception("Invalid return statement");
						}
						return new ReturnValueExpression(argumentExpressions[0]);
					}
					if (keyword == "print")
					{
						if (argumentExpressions.Count != 1)
						{
							throw new Exception("Invalid print call");
						}
						return new CallExpression(UnknownAnalysisType.Instance, Target: new IntrinsicFunctionExpression("print", UnknownAnalysisType.Instance), Args: [argumentExpressions[0]]);
					}
					if (keyword == "if")
					{
						if (argumentExpressions.Count != 2)
						{
							throw new Exception("Invalid if call");
						}
						return new CallExpression(UnknownAnalysisType.Instance, Target: new IntrinsicFunctionExpression("if", UnknownAnalysisType.Instance), Args: [argumentExpressions[0], argumentExpressions[1]]);
					}
					throw new NotImplementedException($"TODO CALLNODE KEYWORD: {keyword}");
				}

				if (targetExpression is MemberMethodGroupExpression { MethodName: { } methodName, Target: { } methodTarget })
				{
					if (methodTarget.Type is not DotnetAnalysisType { Type: { } targetType })
					{
						throw new Exception("Invalid member target type");
					}

					MethodInfo? found = null;
					foreach (var methodInfo in targetType.GetMethods().Where(m => m.Name == methodName))
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

					var foundReturnType = new DotnetAnalysisType(found.ReturnType);
					return new CallDotnetMethodExpression(ReturnType: foundReturnType, MethodInfo: found, Target: methodTarget, Args: [.. argumentExpressions]);
				}

				if (targetExpression is IdentifierExpression { Identifier: { } identifier, Type: { } type })
				{
					if (!scope.TryGetNameTableValue(identifier, out var targetType))
					{
						throw new Exception($"Unknown identifier: {identifier}");
					}
					if (targetType is FunctionAnalysisType { ReturnType: { } returnType })
					{
						return new CallExpression(ReturnType: returnType, Target: targetExpression, Args: [.. argumentExpressions]);
					}
					else
					{
						throw new Exception($"Invalid identifier type: {targetType}");
					}
				}

				throw new NotImplementedException($"TODO: call node -- target={targetExpression}, arguments={String.Join(", ", argumentExpressions.Select(i => $"{i}"))}");
			}
			case AssignNode { Target: { } target, Value: { } value }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue);
				var valueExpression = AnalyzeExpression(value, scope, queue);
				// TODO: TYPE-CHECK
				return new AssignExpression(Type: targetExpression.Type, Target: targetExpression, Value: valueExpression);
			}
			case LogicalNegationNode { Node: { } innerNode }:
			{
				var nodeValue = AnalyzeExpression(innerNode, scope, queue);
				if (nodeValue.Type is not DotnetAnalysisType { Type: { } dotnetType } || dotnetType != typeof(Boolean))
				{
					throw new Exception("Invalid logical negation");
				}
				return new LogicalNegationExpression(nodeValue);
			}
			case ParameterNode:
			{
				return new ParameterExpression();
			}
			case DeclareStaticNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				if (idNode is not Parse.IdentifierNode { Text: { Length: > 0 } id })
				{
					throw new Exception("Invalid identifier");
				}

				if (!scope.NameTable.TryAdd(id, null)) // unknown type until right-hand side is analyzed
				{
					throw new Exception("Duplicate identifier");
				}

				var declType = AnalyzeTypeExpression(typeNode, scope, queue);
				var value = AnalyzeExpression(valueNode, scope, queue);
				var actualType = declType ?? value.Type;
				scope.NameTable[id] = actualType;
				return new DeclarationExpression(actualType, id, value, true);
			}
			case DeclareAssignNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				if (idNode is not Parse.IdentifierNode { Text: { Length: > 0 } id })
				{
					throw new Exception("Invalid identifier");
				}

				if (!scope.NameTable.TryAdd(id, null)) // unknown type until right-hand side is analyzed
				{
					throw new Exception("Duplicate identifier");
				}

				var declType = AnalyzeTypeExpression(typeNode, scope, queue);
				var value = AnalyzeExpression(valueNode, scope, queue);
				var actualType = declType ?? value.Type;
				scope.NameTable[id] = actualType;
				return new DeclarationExpression(actualType, id, value, true);
			}
			default:
			{
				throw new NotImplementedException($"Invalid SyntaxNode for expression: {node}");
			}
		}
	}
}
