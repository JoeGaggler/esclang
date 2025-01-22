namespace EscLang.Analyze;

using System.Reflection;
using EscLang.Parse;
using AnalysisQueue = Queue<Object>; // TODO: strong type

// TODO: constant enforcement
// TODO: collect declarations before analyzing possible references
// TODO: combine call node handling across step/non-step methods

public static class Analyzer
{
	private static Int32 ScopeCounter = -1;

	public static Analysis Analyze(Parse.EscFile file, StreamWriter log)
	{
		var globalScope = new Scope(++ScopeCounter);
		var queue = new AnalysisQueue();

		var mainFunc = (FunctionExpression)AnalyzeExpression(file, globalScope, queue, log);
		var mainFunc2 = (FunctionExpression)TypeCheck(mainFunc, globalScope);
		// mainFunc2 = (FunctionExpression)TypeCheck(mainFunc2, globalScope);
		// mainFunc2 = (FunctionExpression)TypeCheck(mainFunc2, globalScope);
		// mainFunc2 = (FunctionExpression)TypeCheck(mainFunc2, globalScope);

		Analysis analysis = new(Main: mainFunc2.Scope);
		return analysis;
	}

	private static TypedExpression TypeCheck(TypedExpression expression, Scope scope)
	{
		switch (expression)
		{
			case VoidExpression { }: { return expression; }
			case BooleanLiteralExpression { }: { return expression; }
			case IntLiteralExpression { }: { return expression; }
			case StringLiteralExpression { }: { return expression; }
			case ParameterExpression { }: { return expression; }

			case FunctionExpression { ReturnType: { } returnType, Scope: { } funcScope }:
			{
				AnalysisType? newReturnType = null;

				var newExpressions = new List<TypedExpression>();
				foreach (var innerExpr in funcScope.Expressions)
				{
					var newExpression = TypeCheck(innerExpr, funcScope);
					if (newExpression is ReturnValueExpression { Type: { } retType } && newReturnType is null)
					{
						newReturnType = retType;
					}
					newExpressions.Add(newExpression);
				}
				funcScope.Expressions = newExpressions;
				return new FunctionExpression(funcScope, newReturnType ?? UnknownAnalysisType.Instance);
			}
			case DeclarationExpression { Type: { } declType, Value: { } value, Identifier: { } id, IsStatic: { } isStatic }:
			{
				// TODO: not implemented yet
				var newValue = TypeCheck(value, scope);
				var actualType = (declType, newValue.Type) switch
				{
					(UnknownAnalysisType _, _) => newValue.Type,
					_ => declType,
				};
				scope.NameTable[id] = actualType;
				return new DeclarationExpression(actualType, id, newValue, isStatic);
			}
			case CallExpression { Type: { } callType, Args: { } args, ReturnType: { } returnType, Target: { } target }:
			{
				// TODO: not implemented yet

				var newArgs = new List<TypedExpression>();
				foreach (var arg in args)
				{
					var newArg = TypeCheck(arg, scope);
					newArgs.Add(newArg);
				}

				var newTarget = TypeCheck(target, scope);
				if (newTarget is IdentifierExpression { Identifier: { } identifier, Type: { } type })
				{
					if (!scope.TryGetNameTableValue(identifier, out var targetType))
					{
						throw new Exception($"Unknown identifier: {identifier}");
					}
					if (targetType is FunctionAnalysisType { ReturnType: { } newRetType })
					{
						return new CallExpression(ReturnType: newRetType, Target: newTarget, Args: [.. newArgs]);
					}
					else
					{
						throw new Exception($"Invalid identifier type: {targetType}");
					}
					// return new CallExpression(ReturnType: UnknownAnalysisType.Instance, Target: targetExpression, Args: [.. argumentExpressions]);
				}

				return new CallExpression(callType, Target: newTarget, Args: [.. newArgs]);
			}
			// TODO: CallDotnetMethodExpression
			// case CallDotnetMethodExpression { ReturnType: { } callType, MethodInfo: { } methodInfo, Args: { } args, Target: { } methodTarget }:
			// {
			// 	// TODO: not implemented yet
			// 	var newTarg = TypeCheck(methodTarget, scope);
			// 	var newArgs = new List<TypedExpression>();
			// 	foreach (var arg in args)
			// 	{
			// 		var newArg = TypeCheck(arg, scope);
			// 		newArgs.Add(newArg);
			// 	}
			// 	return new CallDotnetMethodExpression(callType, MethodInfo: methodInfo, Target: newTarg, Args: [.. newArgs]);

			// 	if (methodTarget.Type is not DotnetAnalysisType { Type: { } targetType })
			// 	{
			// 		throw new Exception("Invalid member target type");
			// 	}

			// 	MethodInfo? found = null;
			// 	foreach (var methodInfo in targetType.GetMethods().Where(m => m.Name == methodName))
			// 	{
			// 		// TODO: check argument types
			// 		if (methodInfo.GetParameters().Length != argumentExpressions.Count)
			// 		{
			// 			continue;
			// 		}

			// 		found = methodInfo;
			// 		break;
			// 	}

			// 	if (found is null)
			// 	{
			// 		throw new Exception($"Method not found: {methodName}");
			// 	}

			// 	var foundReturnType = new DotnetAnalysisType(found.ReturnType);
			// 	return new CallDotnetMethodExpression(ReturnType: foundReturnType, MethodInfo: found, Target: methodTarget, Args: [.. argumentExpressions]);
			// }
			case ReturnValueExpression { ReturnValue: { } returnValue, Type: { } returnType }:
			{
				// TODO: not implemented yet
				var newReturnValue = TypeCheck(returnValue, scope);
				return new ReturnValueExpression(newReturnValue);
			}
			case AddExpression { Type: { } addType, Left: { } left, Right: { } right }:
			{
				// TODO: not implemented yet
				var newLeft = TypeCheck(left, scope);
				var newRight = TypeCheck(right, scope);

				if (newLeft.Type != newRight.Type)
				{
					throw new Exception($"Type mismatch: left={newLeft.Type}, right={newRight.Type}");
				}

				var newAddType = newLeft.Type; // assuming result is same type as operands

				return new AddExpression(newAddType, Left: newLeft, Right: newRight);
			}
			case AssignExpression { Type: { } assignType, Target: { } target, Value: { } value }:
			{
				// TODO: not implemented yet
				var newTarget = TypeCheck(target, scope);
				var newValue = TypeCheck(value, scope);
				return new AssignExpression(assignType, Target: newTarget, Value: newValue);
			}
			case LogicalNegationExpression { Node: { } node, Type: { } type }:
			{
				// TODO: not implemented yet
				var newNode = TypeCheck(node, scope);
				if (newNode.Type is not DotnetAnalysisType { Type: { } dotnetType } || dotnetType != typeof(Boolean))
				{
					throw new Exception("Invalid logical negation");
				}
				return new LogicalNegationExpression(newNode);
			}
			case IdentifierExpression { Identifier: { } id, Type: { } idType }:
			{
				// scoped identifiers
				if (!scope.TryGetNameTableValue(id, out var type))
				{
					// log.WriteLine($"{scope.Id:0000} queued: id={id}"); // TODO: queue for later analysis, must also have a failure path
					throw new Exception($"Unknown identifier: {id}");
				}
				else if (type is null)
				{
					// log.WriteLine($"{scope.Id:0000} queued: typeof id={id}"); // TODO: queue for later analysis, must also have a failure path
					throw new Exception("Unknown identifier type");
				}

				if (type is FunctionAnalysisType { ReturnType: { } returnType })
				{
					type = returnType;
				}

				return new IdentifierExpression(type, Identifier: id);
			}
			case IntrinsicFunctionExpression { Name: { } name, Type: { } intrinsicType }:
			{
				// TODO: not implemented yet
				return expression;
			}
			default: { throw new NotImplementedException($"TODO: TypeCheck: {expression}"); }
		}
	}

	// TODO: all braces are functions for now, future: InlineScopeExpression
	private static FunctionExpression AnalyzeScope(List<SyntaxNode> nodes, Scope parentScope, AnalysisQueue queue, StreamWriter log)
	{
		var scopeId = ++ScopeCounter;
		log.WriteLine($"{scopeId:0000} scope: nodes={nodes.Count}");
		// TODO: collect declarations
		// TODO: analyze return type

		var innerScope = new Scope(scopeId) { Parent = parentScope };

		foreach (var node in nodes)
		{
			var targetResult = AnalyzeExpression(node, innerScope, queue, log);
			if (targetResult is KeywordExpression { Keyword: "return" })
			{
				innerScope.Expressions.Add(ReturnValueExpression.VoidInstance);
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

	private static AnalysisType? AnalyzeTypeExpression(SyntaxNode? node, Scope scope, AnalysisQueue queue, StreamWriter log)
	{
		log.WriteLine($"{scope.Id:0000} type: {node}");
		if (node is null) { return null; }
		if (node is IdentifierNode { Text: { } id })
		{
			if (id == "bool") { return new DotnetAnalysisType(typeof(Boolean)); }
			if (id == "int") { return new DotnetAnalysisType(typeof(Int32)); }
			if (id == "string") { return new DotnetAnalysisType(typeof(String)); }
		}
		return null;
	}

	private static TypedExpression AnalyzeExpression(SyntaxNode? node, Scope scope, AnalysisQueue queue, StreamWriter log)
	{
		log.WriteLine($"{scope.Id:0000} expression: {node}");
		switch (node)
		{
			case EscFile { Lines: { } lines }: { return AnalyzeScope(lines, scope, queue, log); }
			case LiteralStringNode { Text: { Length: > 0 } stringLiteral }: { return new StringLiteralExpression(stringLiteral); }
			case BracesNode { Lines: { } lines }: { return AnalyzeScope(lines, scope, queue, log); }
			case ParameterNode: { return new ParameterExpression(UnknownAnalysisType.Instance); }
			case LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var intVal = Int32.Parse(numberLiteral);
				return new IntLiteralExpression(intVal);
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

				// type check in second pass
				var type = UnknownAnalysisType.Instance;
				return new IdentifierExpression(type, Identifier: id);
			}
			case PlusNode { Left: { } left, Right: { } right }:
			{
				var leftValue = AnalyzeExpression(left, scope, queue, log);
				var rightValue = AnalyzeExpression(right, scope, queue, log);

				// type check in second pass
				var addType = UnknownAnalysisType.Instance;
				return new AddExpression(addType, Left: leftValue, Right: rightValue);
			}

			case MemberNode { Target: { } target, Member: { } member }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue, log);

				if (member is not IdentifierNode { Text: { Length: > 0 } memberId })
				{
					throw new Exception("Invalid member identifier");
				}

				// TODO: Assuming member is method for now
				return new MemberMethodGroupExpression(Target: targetExpression, MethodName: memberId);
			}
			case CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue, log);

				var argumentExpressions = new List<TypedExpression>();
				foreach (var arg in arguments)
				{
					var argValue = AnalyzeExpression(arg, scope, queue, log);
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
						return new CallExpression(AnalysisType.String, Target: new IntrinsicFunctionExpression("print", AnalysisType.String), Args: [argumentExpressions[0]]);
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
				// if (targetExpression is MemberMethodGroupExpression { MethodName: { } methodName, Target: { } methodTarget })
				// {
				// 	if (methodTarget.Type is not DotnetAnalysisType { Type: { } targetType })
				// 	{
				// 		throw new Exception("Invalid member target type");
				// 	}

				// 	MethodInfo? found = null;
				// 	foreach (var methodInfo in targetType.GetMethods().Where(m => m.Name == methodName))
				// 	{
				// 		// TODO: check argument types
				// 		if (methodInfo.GetParameters().Length != argumentExpressions.Count)
				// 		{
				// 			continue;
				// 		}

				// 		found = methodInfo;
				// 		break;
				// 	}

				// 	if (found is null)
				// 	{
				// 		throw new Exception($"Method not found: {methodName}");
				// 	}

				// 	var foundReturnType = new DotnetAnalysisType(found.ReturnType);
				// 	return new CallDotnetMethodExpression(ReturnType: foundReturnType, MethodInfo: found, Target: methodTarget, Args: [.. argumentExpressions]);
				// }

				return new CallExpression(ReturnType: UnknownAnalysisType.Instance, Target: targetExpression, Args: [.. argumentExpressions]);
			}
			case AssignNode { Target: { } target, Value: { } value }:
			{
				var targetExpression = AnalyzeExpression(target, scope, queue, log);
				var valueExpression = AnalyzeExpression(value, scope, queue, log);
				// TODO: TYPE-CHECK
				return new AssignExpression(Type: targetExpression.Type, Target: targetExpression, Value: valueExpression);
			}
			case LogicalNegationNode { Node: { } innerNode }:
			{
				var nodeValue = AnalyzeExpression(innerNode, scope, queue, log);
				return new LogicalNegationExpression(nodeValue);
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

				var declType = AnalyzeTypeExpression(typeNode, scope, queue, log);
				var value = AnalyzeExpression(valueNode, scope, queue, log);
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

				var declType = AnalyzeTypeExpression(typeNode, scope, queue, log) ?? UnknownAnalysisType.Instance; // type is refined in second pass
				var value = AnalyzeExpression(valueNode, scope, queue, log);
				scope.NameTable[id] = declType;
				return new DeclarationExpression(declType, id, value, true);
			}
			default:
			{
				throw new NotImplementedException($"Invalid SyntaxNode for expression: {node}");
			}
		}
	}
}
