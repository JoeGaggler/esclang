using EscLang.Analyze;

namespace EscLang.Eval;

// TODO: programOutput is only needed for print statements, should be removed from method signatures

public static class Evaluator
{
	public static ExpressionResult Evaluate(Analyze.Analysis file, StringWriter programOutput)
	{
		var globalTable = new ValueTable();

		return EvaluateScope(file.Main, globalTable, programOutput);
	}

	private static ExpressionResult EvaluateScope(Analyze.Scope scope, ValueTable parentTable, StringWriter programOutput)
	{
		var table = new ValueTable(parentTable);
		foreach (var step in scope.Expressions)
		{
			_ = EvaluateTypedExpression(step, table, programOutput);
		}

		return new ImplicitVoidExpressionResult();
	}

	private static ExpressionResult CreateExpressionResult(AnalysisType analysisType, Object? value)
	{
		if (analysisType is not DotnetAnalysisType { Type: { } type })
		{
			throw new NotImplementedException($"Invalid analysis type: {analysisType}");
		}

		// TODO: handle null
		return type.Name switch
		{
			"Int32" => new IntExpressionResult((Int32)value),
			"String" => new StringExpressionResult((String)value),
			_ => throw new NotImplementedException($"Invalid expression result type: {type}"),
		};
	}

	private static Object EvaluateExpressionResult(ExpressionResult value)
	{
		return value switch
		{
			IntExpressionResult intExpressionResult => intExpressionResult.Value,
			StringExpressionResult stringExpressionResult => stringExpressionResult.Value,
			BooleanExpressionResult booleanExpressionResult => booleanExpressionResult.Value,
			_ => throw new NotImplementedException($"Invalid expression result: {value}"),
		};
	}

	private static ExpressionResult EvaluateTypedExpressionAndCall(TypedExpression value, ValueTable table, StringWriter programOutput)
	{
		var result = EvaluateTypedExpression(value, table, programOutput);
		if (result is FunctionExpressionResult { Func: { } func })
		{
			// bare function expression becomes a function call with no arguments
			return CallFunctionExpression(func, [], table, programOutput);
		}
		return result;
	}


	private static ExpressionResult EvaluateTypedExpression(TypedExpression value, ValueTable table, StringWriter programOutput)
	{
		return value switch
		{
			IntLiteralExpression intLiteralExpression => new IntExpressionResult(intLiteralExpression.Value),
			StringLiteralExpression stringLiteralExpression => new StringExpressionResult(stringLiteralExpression.Value),
			BooleanLiteralExpression booleanLiteralExpression => new BooleanExpressionResult(booleanLiteralExpression.Value),
			IdentifierExpression identifierExpression => EvaluateIdentifierExpression(identifierExpression, table, programOutput),
			AddExpression addExpression => EvaluateAddExpression(addExpression, table, programOutput),
			FunctionExpression funcScopeExp => EvaluateFunctionExpression(funcScopeExp, table, programOutput), // TODO: brace scope without function
			CallDotnetMethodExpression callExpression => EvaluateCallDotnetMethodExpression(callExpression, table, programOutput),
			CallExpression callExpression => EvaluateCallExpression(callExpression, table, programOutput),
			AssignExpression assignExpression => EvaluateAssignExpression(assignExpression, table, programOutput),
			LogicalNegationExpression logicalNegationExpression => EvaluateLogicalNegationExpression(logicalNegationExpression, table, programOutput),
			ParameterExpression parameterExpression => EvaluateParameterExpression(parameterExpression, table, programOutput),
			IntrinsicFunctionExpression intrinsicFunctionExpression => EvaluateIntrinsicFunctionExpression(intrinsicFunctionExpression, table, programOutput),
			ReturnExpression returnExpression => EvaluateReturnExpression(returnExpression, table, programOutput),
			DeclarationExpression declarationExpression => EvaluateDeclarationExpression(declarationExpression, table, programOutput),
			_ => throw new NotImplementedException($"Invalid typed expression: {value}"),
		};
	}

	private static ExpressionResult EvaluateDeclarationExpression(DeclarationExpression declarationExpression, ValueTable table, StringWriter programOutput)
	{
		var rhs = EvaluateTypedExpression(declarationExpression.Value, table, programOutput);
		table.Add(declarationExpression.Identifier, rhs);
		return rhs; // TODO: return l-value?
	}

	private static ExpressionResult EvaluateReturnExpression(ReturnExpression returnExpression, ValueTable table, StringWriter programOutput)
	{
		return new ReturnExpressionResult(EvaluateTypedExpression(returnExpression.ReturnValue, table, programOutput));
	}

	private static ExpressionResult EvaluateIntrinsicFunctionExpression(IntrinsicFunctionExpression intrinsicFunctionExpression, ValueTable table, StringWriter programOutput)
	{
		return new IntrinsicFunctionExpressionResult(intrinsicFunctionExpression.Name);
	}

	private static ExpressionResult EvaluateParameterExpression(ParameterExpression parameterExpression, ValueTable table, StringWriter programOutput)
	{
		var parameter = table.GetNextParameter();
		return parameter;
	}

	private static ExpressionResult EvaluateLogicalNegationExpression(LogicalNegationExpression logicalNegationExpression, ValueTable table, StringWriter programOutput)
	{
		var node = EvaluateTypedExpression(logicalNegationExpression.Node, table, programOutput);
		if (node is not BooleanExpressionResult booleanExpressionResult)
		{
			throw new NotImplementedException($"Invalid logical negation expression: {node}");
		}
		return new BooleanExpressionResult(!booleanExpressionResult.Value);
	}

	private static ExpressionResult EvaluateAssignExpression(AssignExpression assignExpression, ValueTable table, StringWriter programOutput)
	{
		// the only l-value we support is an identifier
		if (assignExpression.Target is not IdentifierExpression identifierExpression)
		{
			throw new NotImplementedException($"Invalid assign expression target: {assignExpression.Target}");
		}

		var rhs = EvaluateTypedExpression(assignExpression.Value, table, programOutput);
		table.Set(identifierExpression.Identifier, rhs);
		return rhs; // TODO: return l-value?
	}

	private static ExpressionResult EvaluateCallExpression(CallExpression callExpression, ValueTable table, StringWriter programOutput)
	{
		// CallExpression { 
		//	Type = System.String, 
		// 	ReturnType = System.String, 
		// 	MethodInfo = System.String ToString(System.String, System.IFormatProvider)
		//	Target = IdentifierExpression { Type = System.Int32, Identifier = b }, 
		//	Args = EscLang.Analyze.TypedExpression[] 
		// }
		if (callExpression is not { Type: { } returnType, Target: { } methodTarget })
		{
			throw new NotImplementedException($"Invalid call expression: {callExpression}");
		}
		var args = new ExpressionResult[callExpression.Args.Length];
		foreach (var (i, arg) in callExpression.Args.Index())
		{
			var argExp = EvaluateTypedExpression(arg, table, programOutput);
			args[i] = argExp;
		}
		var targetExpression = EvaluateTypedExpression(methodTarget, table, programOutput);
		if (targetExpression is IntrinsicFunctionExpressionResult { Name: { } intrinsic })
		{
			switch (intrinsic)
			{
				case "print":
				{
					var rhs = args[0];
					var val = rhs switch
					{
						IntExpressionResult intExpressionResult => intExpressionResult.Value.ToString(),
						StringExpressionResult stringExpressionResult => stringExpressionResult.Value,
						_ => throw new NotImplementedException($"Invalid print value: {rhs}"),
					};
					programOutput.WriteLine(val);
					return rhs;
				}
				case "if":
				{
					var condition = args[0];
					var ifBlock = callExpression.Args[1];
					if (condition is not BooleanExpressionResult booleanExpressionResult)
					{
						throw new NotImplementedException($"Invalid if condition: {condition}");
					}
					if (!booleanExpressionResult.Value)
					{
						return new ImplicitVoidExpressionResult();
					}
					if (ifBlock is not FunctionExpression functionScopeExpression)
					{
						throw new NotImplementedException($"Invalid if block: {ifBlock}");
					}
					return EvaluateSharedScopeExpression(functionScopeExpression, table, programOutput);
				}
				default:
				{
					throw new NotImplementedException($"Invalid intrinsic function: {intrinsic}");
				}
			}
		}
		else if (targetExpression is FunctionExpressionResult { Func: { } func })
		{
			// TODO: parameters
			var returnExpression = CallFunctionExpression(func, args, table, programOutput);
			return returnExpression;
		}
		else
		{
			throw new NotImplementedException($"Invalid call target: {methodTarget}");
		}
	}

	private static ExpressionResult EvaluateCallDotnetMethodExpression(CallDotnetMethodExpression callExpression, ValueTable table, StringWriter programOutput)
	{
		if (callExpression is not { Type: { } returnType, MethodInfo: { } methodInfo, Target: { } methodTarget })
		{
			throw new NotImplementedException($"Invalid call expression: {callExpression}");
		}
		var args = new Object[callExpression.Args.Length];
		foreach (var (i, arg) in callExpression.Args.Index())
		{
			var argExp = EvaluateTypedExpression(arg, table, programOutput);
			var argObj = EvaluateExpressionResult(argExp);
			args[i] = argObj;
		}
		var targetExpression = EvaluateTypedExpression(methodTarget, table, programOutput);
		var targetObject = EvaluateExpressionResult(targetExpression);
		var returnValue = methodInfo.Invoke(targetObject, args);
		var returnExpression = CreateExpressionResult(returnType, returnValue);
		return returnExpression;
	}

	private static ExpressionResult EvaluateSharedScopeExpression(FunctionExpression funcScopeExp, ValueTable table, StringWriter programOutput)
	{
		foreach (var step in funcScopeExp.Scope.Expressions)
		{
			var stepNode = EvaluateTypedExpression(step, table, programOutput);
			if (stepNode is ReturnExpressionResult or ReturnVoidResult)
			{
				return stepNode; // pass return result to parent scope until a function scope is reached
			}
		}
		return new ReturnVoidResult();
	}

	private static ExpressionResult EvaluateFunctionExpression(FunctionExpression functionExpression, ValueTable table, StringWriter programOutput)
	{
		return new FunctionExpressionResult(functionExpression);
	}

	private static ExpressionResult CallFunctionExpression(FunctionExpression functionExpression, ExpressionResult[] args, ValueTable table, StringWriter programOutput)
	{
		var innerValueTable = new ValueTable(table);
		innerValueTable.SetArguments(args.ToList());
		foreach (var step in functionExpression.Scope.Expressions)
		{
			var stepNode = EvaluateTypedExpression(step, innerValueTable, programOutput);
			if (stepNode is ReturnExpressionResult ret)
			{
				return ret.Value; // unwrap return result, returns do not propagate outside of current function
			}
			if (stepNode is ReturnVoidResult)
			{
				return new ImplicitVoidExpressionResult(); // return void, returns do not propagate outside of current function
			}
		}
		return new ReturnVoidResult();
	}

	private static ExpressionResult EvaluateAddExpression(AddExpression addExpression, ValueTable table, StringWriter programOutput)
	{
		var left = EvaluateTypedExpressionAndCall(addExpression.Left, table, programOutput);
		var right = EvaluateTypedExpressionAndCall(addExpression.Right, table, programOutput);

		var leftObj = EvaluateExpressionResult(left);
		var rightObj = EvaluateExpressionResult(right);

		if (addExpression.Type is not DotnetAnalysisType { Type: { } dotnetType } || dotnetType != typeof(Int32))
		{
			throw new NotImplementedException($"Invalid add expression type: {addExpression.Type}");
		}
		if (leftObj is not Int32 leftInt)
		{
			throw new NotImplementedException($"Invalid add expression left: {left}");
		}
		if (rightObj is not Int32 rightInt)
		{
			throw new NotImplementedException($"Invalid add expression right: \n{right}\n{rightObj}");
		}
		var sum = leftInt + rightInt;
		return new IntExpressionResult(sum);
	}

	private static ExpressionResult EvaluateIdentifierExpression(IdentifierExpression identifierExpression, ValueTable table, StringWriter programOutput)
	{
		var id = identifierExpression.Identifier;
		if (table.Get(id) is { } value)
		{
			return value;
		}
		else
		{
			throw new Exception($"Unknown identifier: {id}");
		}
	}
}
