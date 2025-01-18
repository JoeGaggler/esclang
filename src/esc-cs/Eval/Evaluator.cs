using EscLang.Analyze;

namespace EscLang.Eval;

// TODO: combine scope evaluation functions
// TODO: programOutput is only needed for print statements, should be removed from method signatures

public static class Evaluator
{
	public static Evaluation Evaluate(Analyze.Analysis file, StringWriter programOutput)
	{
		var globalTable = new ValueTable();

		var scope = file.Main;
		var table = new ValueTable(globalTable);
		foreach (var step in scope.Expressions)
		{
			var result = EvaluateTypedExpression(step, table, programOutput);
			if (result is ReturnValueEvaluation ret)
			{
				return ret.Value;
			}
		}

		return VoidEvaluation.Instance;
	}

	private static Evaluation CreateExpressionResult(AnalysisType analysisType, Object? value)
	{
		if (analysisType is not DotnetAnalysisType { Type: { } type })
		{
			throw new NotImplementedException($"Invalid analysis type: {analysisType}");
		}

		// TODO: handle null
		return type.Name switch
		{
			"Int32" => new IntEvaluation((Int32)value),
			"String" => new StringEvaluation((String)value),
			_ => throw new NotImplementedException($"Invalid expression result type: {type}"),
		};
	}

	private static Object EvaluateExpressionResult(Evaluation value)
	{
		return value switch
		{
			IntEvaluation intExpressionResult => intExpressionResult.Value,
			StringEvaluation stringExpressionResult => stringExpressionResult.Value,
			BooleanEvaluation booleanExpressionResult => booleanExpressionResult.Value,
			_ => throw new NotImplementedException($"Invalid expression result: {value}"),
		};
	}

	private static Evaluation EvaluateTypedExpressionAndCall(TypedExpression value, ValueTable table, StringWriter programOutput)
	{
		var result = EvaluateTypedExpression(value, table, programOutput);
		if (result is FunctionExpressionEvaluation { Func: { } func })
		{
			// bare function expression becomes a function call with no arguments
			return CallFunctionExpression(func, [], table, programOutput);
		}
		return result;
	}


	private static Evaluation EvaluateTypedExpression(TypedExpression value, ValueTable table, StringWriter programOutput)
	{
		return value switch
		{
			IntLiteralExpression intLiteralExpression => new IntEvaluation(intLiteralExpression.Value),
			StringLiteralExpression stringLiteralExpression => new StringEvaluation(stringLiteralExpression.Value),
			BooleanLiteralExpression booleanLiteralExpression => new BooleanEvaluation(booleanLiteralExpression.Value),
			IdentifierExpression identifierExpression => EvaluateIdentifierExpression(identifierExpression, table, programOutput),
			AddExpression addExpression => EvaluateAddExpression(addExpression, table, programOutput),
			FunctionExpression funcScopeExp => EvaluateFunctionExpression(funcScopeExp, table, programOutput), // TODO: brace scope without function
			CallDotnetMethodExpression callExpression => EvaluateCallDotnetMethodExpression(callExpression, table, programOutput),
			CallExpression callExpression => EvaluateCallExpression(callExpression, table, programOutput),
			AssignExpression assignExpression => EvaluateAssignExpression(assignExpression, table, programOutput),
			LogicalNegationExpression logicalNegationExpression => EvaluateLogicalNegationExpression(logicalNegationExpression, table, programOutput),
			ParameterExpression parameterExpression => EvaluateParameterExpression(parameterExpression, table, programOutput),
			IntrinsicFunctionExpression intrinsicFunctionExpression => EvaluateIntrinsicFunctionExpression(intrinsicFunctionExpression, table, programOutput),
			ReturnValueExpression returnExpression => EvaluateReturnExpression(returnExpression, table, programOutput),
			ReturnVoidExpression returnExpression => EvaluateReturnVoidExpression(returnExpression, table, programOutput),
			DeclarationExpression declarationExpression => EvaluateDeclarationExpression(declarationExpression, table, programOutput),
			_ => throw new NotImplementedException($"Invalid typed expression: {value}"),
		};
	}

	private static Evaluation EvaluateDeclarationExpression(DeclarationExpression declarationExpression, ValueTable table, StringWriter programOutput)
	{
		var rhs = EvaluateTypedExpression(declarationExpression.Value, table, programOutput);
		table.Add(declarationExpression.Identifier, rhs);
		return rhs; // TODO: return l-value?
	}

	private static Evaluation EvaluateReturnExpression(ReturnValueExpression returnExpression, ValueTable table, StringWriter programOutput)
	{
		return new ReturnValueEvaluation(EvaluateTypedExpression(returnExpression.ReturnValue, table, programOutput));
	}

	private static Evaluation EvaluateReturnVoidExpression(ReturnVoidExpression returnExpression, ValueTable table, StringWriter programOutput)
	{
		return new ReturnValueEvaluation(VoidEvaluation.Instance);
	}

	private static Evaluation EvaluateIntrinsicFunctionExpression(IntrinsicFunctionExpression intrinsicFunctionExpression, ValueTable table, StringWriter programOutput)
	{
		return new IntrinsicFunctionEvaluation(intrinsicFunctionExpression.Name);
	}

	private static Evaluation EvaluateParameterExpression(ParameterExpression parameterExpression, ValueTable table, StringWriter programOutput)
	{
		var parameter = table.GetNextParameter();
		return parameter;
	}

	private static Evaluation EvaluateLogicalNegationExpression(LogicalNegationExpression logicalNegationExpression, ValueTable table, StringWriter programOutput)
	{
		var node = EvaluateTypedExpression(logicalNegationExpression.Node, table, programOutput);
		if (node is not BooleanEvaluation booleanExpressionResult)
		{
			throw new NotImplementedException($"Invalid logical negation expression: {node}");
		}
		return new BooleanEvaluation(!booleanExpressionResult.Value);
	}

	private static Evaluation EvaluateAssignExpression(AssignExpression assignExpression, ValueTable table, StringWriter programOutput)
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

	private static Evaluation EvaluateCallExpression(CallExpression callExpression, ValueTable table, StringWriter programOutput)
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
		var args = new Evaluation[callExpression.Args.Length];
		foreach (var (i, arg) in callExpression.Args.Index())
		{
			var argExp = EvaluateTypedExpression(arg, table, programOutput);
			args[i] = argExp;
		}
		var targetExpression = EvaluateTypedExpression(methodTarget, table, programOutput);
		if (targetExpression is IntrinsicFunctionEvaluation { Name: { } intrinsic })
		{
			switch (intrinsic)
			{
				case "print":
				{
					var rhs = args[0];
					var val = rhs switch
					{
						IntEvaluation intExpressionResult => intExpressionResult.Value.ToString(),
						StringEvaluation stringExpressionResult => stringExpressionResult.Value,
						_ => throw new NotImplementedException($"Invalid print value: {rhs}"),
					};
					programOutput.WriteLine(val);
					return rhs;
				}
				case "if":
				{
					var condition = args[0];
					var ifBlock = callExpression.Args[1];
					if (condition is not BooleanEvaluation booleanExpressionResult)
					{
						throw new NotImplementedException($"Invalid if condition: {condition}");
					}
					if (!booleanExpressionResult.Value)
					{
						return VoidEvaluation.Instance;
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
		else if (targetExpression is FunctionExpressionEvaluation { Func: { } func })
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

	private static Evaluation EvaluateCallDotnetMethodExpression(CallDotnetMethodExpression callExpression, ValueTable table, StringWriter programOutput)
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

	private static Evaluation EvaluateSharedScopeExpression(FunctionExpression funcScopeExp, ValueTable table, StringWriter programOutput)
	{
		foreach (var step in funcScopeExp.Scope.Expressions)
		{
			var stepNode = EvaluateTypedExpression(step, table, programOutput);
			if (stepNode is ReturnValueEvaluation or ReturnVoidEvaluation)
			{
				return stepNode; // pass return result to parent scope until a function scope is reached
			}
		}
		return new ReturnVoidEvaluation();
	}

	private static Evaluation EvaluateFunctionExpression(FunctionExpression functionExpression, ValueTable table, StringWriter programOutput)
	{
		return new FunctionExpressionEvaluation(functionExpression);
	}

	private static Evaluation CallFunctionExpression(FunctionExpression functionExpression, Evaluation[] args, ValueTable table, StringWriter programOutput)
	{
		var innerValueTable = new ValueTable(table);
		innerValueTable.SetArguments(args.ToList());
		foreach (var step in functionExpression.Scope.Expressions)
		{
			var stepNode = EvaluateTypedExpression(step, innerValueTable, programOutput);
			if (stepNode is ReturnValueEvaluation ret)
			{
				return ret.Value; // unwrap return result, returns do not propagate outside of current function
			}
			if (stepNode is ReturnVoidEvaluation)
			{
				return VoidEvaluation.Instance; // return void, returns do not propagate outside of current function
			}
		}
		return new ReturnVoidEvaluation();
	}

	private static Evaluation EvaluateAddExpression(AddExpression addExpression, ValueTable table, StringWriter programOutput)
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
		return new IntEvaluation(sum);
	}

	private static Evaluation EvaluateIdentifierExpression(IdentifierExpression identifierExpression, ValueTable table, StringWriter programOutput)
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
