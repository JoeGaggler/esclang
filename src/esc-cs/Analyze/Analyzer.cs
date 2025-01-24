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

		log.WriteLine("=== Table ===");
		_ = BuildTable(file, 0, log);

		log.WriteLine("=== Tree ===");
		Printer.PrintTable(log);

		// log.WriteLine("=== Analyze ===");
		// var mainFunc = (FunctionExpression)AnalyzeExpression(file, globalScope, queue, log);

		// log.WriteLine("=== TypeCheck ===");
		// var mainFunc2 = (FunctionExpression)TypeCheck(mainFunc, null, globalScope, log);

		// Analysis analysis = new(Main: mainFunc2.Scope);
		Analysis analysis = new(Main: globalScope);
		return analysis;
	}

	private static int BuildTable(SyntaxNode node, int parentSlot, StreamWriter log)
	{
		switch (node)
		{
			case EscFile { Lines: { } lines }:
			{
				var fileData = new FileSlotData();
				var fileSlot = Table.Instance.Add(parentSlot, TableSlotType.File, fileData, log);

				var bracesData = new BracesSlotData(Lines: []);
				var bracesSlot = Table.Instance.Add(fileSlot, TableSlotType.Braces, bracesData, log);

				fileData = fileData with { Main = bracesSlot };
				Table.Instance.Update(fileSlot, fileData, log);

				// Lines
				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, bracesSlot, log);
					lineSlots.Add(lineSlot);
				}

				bracesData = bracesData with { Lines = [.. lineSlots] };
				Table.Instance.Update(bracesSlot, bracesData, log);

				return fileSlot;
			}
			case DeclareStaticNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(true, parentSlot, log, idNode, typeNode, valueNode);
			}
			case DeclareAssignNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(false, parentSlot, log, idNode, typeNode, valueNode);
			}
			case CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var data = new CallSlotData(Target: 0, Args: []);
				var slot = Table.Instance.Add(parentSlot, TableSlotType.Call, data, log);

				// Target
				var targetSlot = BuildTable(target, slot, log);
				data = data with { Target = targetSlot };
				Table.Instance.Update(slot, data, log);

				// Args
				var argSlots = new List<int>();
				foreach (var arg in arguments)
				{
					var argSlot = BuildTable(arg, slot, log);
					argSlots.Add(argSlot);
				}

				data = data with { Args = [.. argSlots] };
				Table.Instance.Update(slot, data, log);

				return slot;
			}
			case IdentifierNode { Text: { Length: > 0 } id }:
			{
				var data = new IdentifierSlotData(Name: id);
				var slot = Table.Instance.Add(parentSlot, TableSlotType.Identifier, data, log);

				return slot;
			}
			case BracesNode { Lines: { } lines }:
			{
				var data = new BracesSlotData([]);
				var slot = Table.Instance.Add(parentSlot, TableSlotType.Braces, data, log);

				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, slot, log);
					lineSlots.Add(lineSlot);
				}

				data = data with { Lines = [.. lineSlots] };
				Table.Instance.Update(slot, data, log);

				return slot;
			}
			case LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var data = new IntegerSlotData(Value: Int32.Parse(numberLiteral));
				var slot = Table.Instance.Add(parentSlot, TableSlotType.Integer, data, log);

				return slot;
			}
			case PlusNode { Left: { } left, Right: { } right }:
			{
				var data = new AddOpSlotData();
				var slot = Table.Instance.Add(parentSlot, TableSlotType.Add, data, log);

				// Operands
				var leftSlot = BuildTable(left, slot, log);
				var rightSlot = BuildTable(right, slot, log);

				data = data with { Left = leftSlot, Right = rightSlot };
				Table.Instance.Update(slot, data, log);

				return slot;
			}
			default:
			{
				log.WriteLine($"unknown node for table: {node}");
				throw new NotImplementedException($"TODO: BuildTable: {node}");
			}
		}
	}

	private static int BuildDeclareNode(Boolean isStatic, int parentSlot, StreamWriter log, SyntaxNode idNode, SyntaxNode? typeNode, SyntaxNode valueNode)
	{
		if (idNode is not Parse.IdentifierNode { Text: { Length: > 0 } id })
		{
			throw new Exception("Invalid identifier");
		}

		// TODO: add slot, enqueue child nodes, queue update slot with analyzed data
		var data = new DeclareSlotData(Name: id, IsStatic: isStatic);
		var slot = Table.Instance.Add(parentSlot, TableSlotType.Declare, data, log);

		// Name
		if (!Table.Instance.TryGetSlot<BracesSlotData>(parentSlot, TableSlotType.Braces, out var bracesData, log))
		{
			throw new Exception("Invalid parent slot");
		}
		if (!bracesData.TryAddNameTableValue(id, slot))
		{
			throw new Exception("Duplicate identifier");
		}

		// Type
		var typeSlot = 0;
		if (typeNode is not null)
		{
			typeSlot = BuildTable(typeNode, slot, log);
			data = data with { Type = typeSlot };
			Table.Instance.Update(slot, data, log);
		}

		// Value
		var valueSlot = BuildTable(valueNode, slot, log);
		data = data with { Value = valueSlot };
		Table.Instance.Update(slot, data, log);

		return slot;
	}

	private static TypedExpression TypeCheck(TypedExpression expression, TypedExpression? parentExpression, Scope scope, StreamWriter log)
	{
		switch (expression)
		{
			case VoidExpression { }: { return expression; }
			case BooleanLiteralExpression { }: { return expression; }
			case IntLiteralExpression { }: { return expression; }
			case StringLiteralExpression { }: { return expression; }
			case ParameterExpression { }: { return expression; }

			// case DotnetMemberMethodExpression { Target: { } target } methodExpr:
			// {
			// 	var newTarget = TypeCheck(target, expression, scope);
			// 	return methodExpr with { Target = newTarget };
			// }

			case FunctionExpression { ReturnType: { } returnType, Scope: { } funcScope }:
			{
				AnalysisType? newReturnType = null;

				var newExpressions = new List<TypedExpression>();
				foreach (var innerExpr in funcScope.Expressions)
				{
					var newExpression = TypeCheck(innerExpr, expression, funcScope, log);
					if (newExpression is ReturnValueExpression { Type: { } retType } && newReturnType is null)
					{
						newReturnType = retType;
					}
					newExpressions.Add(newExpression);
				}
				funcScope.Expressions = newExpressions;

				var final = new FunctionExpression(funcScope, newReturnType ?? UnknownAnalysisType.Instance);
				log.WriteLine($"{scope.Id:0000} function: {final.Type}");
				return final;
			}
			case DeclarationExpression { Type: { } declType, Value: { } value, Identifier: { } id, IsStatic: { } isStatic }:
			{
				// TODO: not implemented yet
				var newValue = TypeCheck(value, expression, scope, log);
				// Console.WriteLine($"TypeCheck DeclExpr: {id} = {newValue}");

				var actualType = (declType, newValue.Type) switch
				{
					(UnknownAnalysisType _, _) => newValue.Type,
					_ => declType,
				};
				scope.NameTable[id] = actualType;

				var final = new DeclarationExpression(actualType, id, newValue, isStatic);
				log.WriteLine($"{scope.Id:0000} declaration: id={id} type={final.Type}");
				return final;
			}
			case CallExpression { Type: { } callType, Args: { } args, ReturnType: { } returnType, Target: { } target }:
			{
				// TODO: not implemented yet

				var newArgs = new List<TypedExpression>();
				foreach (var arg in args)
				{
					var newArg = TypeCheck(arg, expression, scope, log);
					newArgs.Add(newArg);
				}

				// Partially type-checked call expression used as context for target type-checking (TODO: dependency graph)
				var newCallExpression = new CallExpression(callType, Target: target, Args: [.. newArgs]);

				var newTarget = TypeCheck(target, newCallExpression, scope, log);
				if (newTarget is IdentifierExpression { Identifier: { } identifier, Type: { } type })
				{
					if (!scope.TryGetNameTableValue(identifier, out var targetType))
					{
						throw new Exception($"Unknown identifier: {identifier}");
					}
					if (targetType is FunctionAnalysisType { ReturnType: { } newRetType })
					{
						var final2 = newCallExpression with { ReturnType = newRetType, Type = newRetType };
						log.WriteLine($"{scope.Id:0000} call: {final2.Type}");
						return final2;
					}
					else
					{
						throw new Exception($"Invalid identifier type: {targetType}");
					}
				}

				var final = newCallExpression with { Target = newTarget, ReturnType = newTarget.Type, Type = newTarget.Type };
				log.WriteLine($"{scope.Id:0000} call: {final.Type}");
				return final;
			}
			case MemberExpression { Target: { } target, MemberName: { } methodName, Type: { } type }:
			{
				// TODO: this depends on if the parent is a call expression (method) or not (property)
				// for a call, this also depends on the arguments to disambiguate the method group

				//MemberMethodGroupExpression { Type = UnknownAnalysisType { FullName = Unknown }, Target = IdentifierExpression { Type = UnknownAnalysisType { FullName = Unknown }, Identifier = b }, MethodName = ToString }
				var newTarget = TypeCheck(target, expression, scope, log);
				var targetType = newTarget.Type;
				if (targetType is DotnetAnalysisType { Type: { } dotnetType })
				{
					if (parentExpression is CallExpression { Args: { } argumentExpressions })
					{
						foreach (var methodInfo in dotnetType.GetMethods().Where(m => m.Name == methodName))
						{
							// TODO: check argument types
							if (methodInfo.GetParameters().Length != argumentExpressions.Length)
							{
								continue;
							}

							return new DotnetMemberMethodExpression(ReturnType: new DotnetAnalysisType(methodInfo.ReturnType), MethodInfo: methodInfo, Target: newTarget);
						}

						throw new NotImplementedException($"TODO: TypeCheck MemberExpression not found");
					}
					else
					{
						throw new NotImplementedException($"TODO: TypeCheck MemberExpression parent: {parentExpression}");
					}
				}
				else
				{
					throw new Exception($"Invalid member target type: {targetType}");
				}
			}
			case ReturnValueExpression { ReturnValue: { } returnValue, Type: { } returnType }:
			{
				// TODO: not implemented yet
				var newReturnValue = TypeCheck(returnValue, expression, scope, log);
				var final = new ReturnValueExpression(newReturnValue);
				log.WriteLine($"{scope.Id:0000} return: {final.Type}");
				return final;
			}
			case AddExpression { Type: { } addType, Left: { } left, Right: { } right }:
			{
				// TODO: not implemented yet
				var newLeft = TypeCheck(left, expression, scope, log);
				var newRight = TypeCheck(right, expression, scope, log);

				if (newLeft.Type != newRight.Type)
				{
					throw new Exception($"Type mismatch: left={newLeft.Type}, right={newRight}");
					// Console.WriteLine($"Type mismatch: left={newLeft.Type}, right={newRight}");
				}

				var newAddType = newLeft.Type; // assuming result is same type as operands

				var final = new AddExpression(newAddType, Left: newLeft, Right: newRight);
				log.WriteLine($"{scope.Id:0000} add: {final.Type}");
				return final;
			}
			case AssignExpression { Type: { } assignType, Target: { } target, Value: { } value }:
			{
				// TODO: not implemented yet
				var newTarget = TypeCheck(target, expression, scope, log);
				var newValue = TypeCheck(value, expression, scope, log);
				var final = new AssignExpression(newValue.Type, Target: newTarget, Value: newValue);
				log.WriteLine($"{scope.Id:0000} assign: {final.Type}");
				return final;
			}
			case LogicalNegationExpression { Node: { } node, Type: { } type }:
			{
				// TODO: not implemented yet
				var newNode = TypeCheck(node, expression, scope, log);
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

				var final = new IdentifierExpression(type, Identifier: id);
				log.WriteLine($"{scope.Id:0000} identifier: id={id} type={final.Type}");
				return final;
			}
			case IntrinsicFunctionExpression { Name: { } name, Type: { } intrinsicType }:
			{
				// TODO: not implemented yet
				var final = new IntrinsicFunctionExpression(name, intrinsicType);
				log.WriteLine($"{scope.Id:0000} intrinsic(TODO): {final.Type}");
				return final;
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
		log.WriteLine($"{scope.Id:0000} type: {(node is null ? "null" : node.ToString())}");
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

				var type = UnknownAnalysisType.Instance; // type check in second pass
				return new MemberExpression(type, Target: targetExpression, MemberName: memberId);
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
