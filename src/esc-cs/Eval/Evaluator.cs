using System.Reflection;
using EscLang.Analyze;

namespace EscLang.Eval;

// TODO: out of order function declarations
// TODO: programOutput is only needed for print statements, should be removed from method signatures

public static class Evaluator
{
	public static Evaluation Evaluate(Analysis analysis, StringWriter programOutput)
	{
		var globalTable = new ValueTable();

		var slotId = 1; // TODO: get braces slot id from table
		return EvaluateSlot(slotId, analysis, programOutput, globalTable);
	}

	private static Evaluation CallFunctionSlot(int bracesSlotId, Evaluation[] args, Analysis analysis, StringWriter programOutput, ValueTable valueTable)
	{
		var bracesSlot = analysis.GetSlot(bracesSlotId);
		var bracesData = analysis.GetData<BracesSlotData>(bracesSlotId);

		var bracesValueTable = new ValueTable(valueTable);
		bracesValueTable.SetArguments(args);

		// pass 1: static declarations
		foreach (var step in bracesData.Lines)
		{
			_ = EvaluateSlotStaticOnly(step, analysis, programOutput, bracesValueTable);
		}

		// pass 2: dynamic execution
		foreach (var step in bracesData.Lines)
		{
			var stepNode = EvaluateSlot(step, analysis, programOutput, bracesValueTable);
			if (stepNode is ReturnValueEvaluation ret)
			{
				return ret.Value; // unwrap return result, returns do not propagate outside of current function
			}
			if (stepNode is ReturnVoidEvaluation)
			{
				return VoidEvaluation.Instance; // return void, returns do not propagate outside of current function
			}
		}

		return VoidEvaluation.Instance;
	}

	private static Evaluation CallInlineSlot(int bracesSlotId, Evaluation[] args, Analysis analysis, StringWriter programOutput, ValueTable valueTable)
	{
		var bracesSlot = analysis.GetSlot(bracesSlotId);
		var bracesData = analysis.GetData<BracesSlotData>(bracesSlotId);

		var bracesValueTable = new ValueTable(valueTable);
		bracesValueTable.SetArguments(args);

		foreach (var step in bracesData.Lines)
		{
			var stepNode = EvaluateSlot(step, analysis, programOutput, bracesValueTable);
			if (stepNode is ReturnValueEvaluation ret)
			{
				return stepNode; // returns propagate outside of current function
			}
			if (stepNode is ReturnVoidEvaluation)
			{
				return stepNode; // returns propagate outside of current function
			}
		}

		return VoidEvaluation.Instance;
	}

	private static Evaluation EvaluateSlot(int slotId, Analysis analysis, StringWriter programOutput, ValueTable valueTable)
	{
		var slot = analysis.GetSlot(slotId);
		var slotData = analysis.GetData<SlotData>(slotId);
		switch (slot.CodeType)
		{
			case SlotEnum.Boolean: return new BooleanEvaluation(((BooleanSlotData)slotData).Value);
			case SlotEnum.Integer: return new IntEvaluation(((IntegerSlotData)slotData).Value);
			case SlotEnum.String: return new StringEvaluation(((StringSlotData)slotData).Value);

			case SlotEnum.File:
			{
				var fileData = (FileSlotData)slotData;
				var mainDeclResult = EvaluateSlot(fileData.Main, analysis, programOutput, valueTable);
				if (mainDeclResult is not FunctionEvaluation { BracesSlotId: { } bracesSlotId })
				{
					throw new NotImplementedException($"Invalid main declaration: {mainDeclResult}");
				}

				return CallFunctionSlot(bracesSlotId, [], analysis, programOutput, valueTable);
			}
			case SlotEnum.Braces:
			{
				// var braceData = (Analyze.BracesSlotData)slotData;
				return new FunctionEvaluation(slotId);
			}
			case SlotEnum.Declare:
			{
				var declareData = (DeclareSlotData)slotData;
				if (declareData.IsStatic) { return VoidEvaluation.Instance; } // only evaluate dynamic declarations
				var rhs = EvaluateSlot(declareData.Value, analysis, programOutput, valueTable);
				valueTable.Add(declareData.Name, rhs);
				return rhs; // TODO: return l-value?
			}
			case SlotEnum.Return:
			{
				var returnData = (ReturnSlotData)slotData;
				if (returnData.Value == 0)
				{
					return ReturnVoidEvaluation.Instance;
				}
				var returnValue = EvaluateSlot(returnData.Value, analysis, programOutput, valueTable);
				return new ReturnValueEvaluation(returnValue);
			}
			case SlotEnum.Identifier:
			{
				var identifierData = (IdentifierSlotData)slotData;
				var id = identifierData.Name;
				if (valueTable.Get(id) is { } value)
				{
					return value;
				}
				else
				{
					throw new Exception($"Unknown identifier: {id}");
				}
			}
			case SlotEnum.Assign:
			{
				var assignData = (AssignSlotData)slotData;
				var rhs = EvaluateSlot(assignData.Value, analysis, programOutput, valueTable);

				var idSlot = analysis.GetSlot(assignData.Target);
				var idData = analysis.GetData<IdentifierSlotData>(assignData.Target);
				var id = idData.Name;

				valueTable.Set(id, rhs);
				return rhs; // TODO: return l-value?
			}
			case SlotEnum.Add:
			{
				var addData = (AddSlotData)slotData;
				var left = EvaluateSlot(addData.Left, analysis, programOutput, valueTable);
				var right = EvaluateSlot(addData.Right, analysis, programOutput, valueTable);

				var intTypeSlotId = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(int)), StreamWriter.Null);
				if (slot.TypeSlot != intTypeSlotId)
				{
					throw new NotImplementedException($"Invalid add expression type: {slot} {left} {right}");
				}

				var leftObj = (IntEvaluation)left;
				var rightObj = (IntEvaluation)right;

				// if (addData.Type is not DotnetAnalysisType { Type: { } dotnetType } || dotnetType != typeof(Int32))
				// {
				// 	throw new NotImplementedException($"Invalid add expression type: {addData.Type}");
				// }
				// if (leftObj is not Int32 leftInt)
				// {
				// 	throw new NotImplementedException($"Invalid add expression left: {left}");
				// }
				// if (rightObj is not Int32 rightInt)
				// {
				// 	throw new NotImplementedException($"Invalid add expression right: \n{right}\n{rightObj}");
				// }
				var sum = leftObj.Value + rightObj.Value;
				return new IntEvaluation(sum);
			}
			case SlotEnum.Intrinsic:
			{
				var intrinsicData = (IntrinsicSlotData)slotData;
				return new IntrinsicFunctionEvaluation(intrinsicData.Name);
			}
			case SlotEnum.Parameter:
			{
				var parameterData = (ParameterSlotData)slotData;
				var parameter = valueTable.GetNextParameter();
				return parameter;
			}
			case SlotEnum.LogicalNegation:
			{
				var logicalNegationData = (LogicalNegationSlotData)slotData;
				var value = EvaluateSlot(logicalNegationData.Value, analysis, programOutput, valueTable);
				if (value is not BooleanEvaluation booleanExpressionResult)
				{
					throw new NotImplementedException($"Invalid logical negation value: {value}");
				}
				return new BooleanEvaluation(!booleanExpressionResult.Value);
			}
			case SlotEnum.Negation:
			{
				var negationData = (NegationSlotData)slotData;
				var value = EvaluateSlot(negationData.Value, analysis, programOutput, valueTable);
				if (value is not IntEvaluation intExpressionResult)
				{
					throw new NotImplementedException($"Invalid negation value: {value}");
				}
				return new IntEvaluation(-intExpressionResult.Value);
			}
			case SlotEnum.Call:
			{
				var callData = (CallSlotData)slotData;
				var args = new Evaluation[callData.Args.Length];
				foreach (var (i, arg) in callData.Args.Index())
				{
					var argExp = EvaluateSlot(arg, analysis, programOutput, valueTable);
					args[i] = argExp;
				}
				var targetExpression = EvaluateSlot(callData.Target, analysis, programOutput, valueTable);
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
						default:
						{
							throw new NotImplementedException($"Invalid intrinsic function: {intrinsic}");
						}
					}
				}
				else if (targetExpression is MemberEvaluation { Target: { } target, Name: { } memberName })
				{
					if (callData.DotnetMethod is not { } dotnetMethod)
					{
						throw new NotImplementedException($"Invalid call target without dotnet method: {targetExpression}");
					}

					// Determine dotnet type for each argument
					var argTypes = new Type[args.Length];
					foreach (var (i, arg) in args.Index())
					{
						argTypes[i] = arg switch
						{
							IntEvaluation => typeof(int),
							StringEvaluation => typeof(string),
							_ => throw new NotImplementedException($"Invalid call argument: {arg}"),
						};
					}

					var targetObject = ObjFromEval(target);
					var dotnetArgs = args.Select(i => ObjFromEval(i)).ToArray();
					var returnValue = dotnetMethod.Invoke(targetObject, dotnetArgs);
					var returnEval = EvalFromObj(returnValue);

					return returnEval;
				}
				else if (targetExpression is FunctionEvaluation { BracesSlotId: { } bracesSlotId })
				{
					// TODO: ARGS!
					var returnExpression = CallFunctionSlot(bracesSlotId, args, analysis, programOutput, valueTable);
					return returnExpression;
				}
				else
				{
					throw new NotImplementedException($"Invalid call target: {targetExpression}");
				}
			}
			case SlotEnum.If:
			{
				var ifData = (IfSlotData)slotData;
				var condition = EvaluateSlot(ifData.Condition, analysis, programOutput, valueTable);
				if (condition is not BooleanEvaluation booleanExpressionResult)
				{
					throw new NotImplementedException($"Invalid if condition: {condition}");
				}
				if (!booleanExpressionResult.Value)
				{
					return VoidEvaluation.Instance;
				}
				var ifBodyResult = CallInlineSlot(ifData.Body, [], analysis, programOutput, valueTable);
				return ifBodyResult;
			}
			case SlotEnum.Member:
			{
				var memberData = (MemberSlotData)slotData;
				var memberName = analysis.GetData<IdentifierSlotData>(memberData.Member).Name;
				var target = EvaluateSlot(memberData.Target, analysis, programOutput, valueTable);
				var dotnetTarget = ObjFromEval(target);
				var memberType = analysis.GetSlot(slot.TypeSlot);
				if (memberType.Data is DotnetMemberTypeSlotData { TargetType: { } targetType, MemberType: MemberTypes.Property, Members: { } propertyInfos })
				{
					if (propertyInfos is not [PropertyInfo propertyInfo, ..])
					{
						throw new NotImplementedException($"Invalid property info: {propertyInfos.Length}");
					}
					var dotnetValue = propertyInfo.GetValue(dotnetTarget);
					var evalValue = EvalFromObj(dotnetValue);
					return evalValue;
				}
				else
				{
					return new MemberEvaluation(target, memberName);
				}
			}
			case SlotEnum.Void:
			{
				return VoidEvaluation.Instance;
			}
			default:
			{
				throw new InvalidOperationException($"Invalid slot type: {slot}");
			}
		}
	}

	private static Evaluation EvaluateSlotStaticOnly(int slotId, Analysis analysis, StringWriter programOutput, ValueTable valueTable)
	{
		var slot = analysis.GetSlot(slotId);
		var slotData = analysis.GetData<SlotData>(slotId);
		switch (slot.CodeType)
		{
			case SlotEnum.Declare:
			{
				var declareData = (DeclareSlotData)slotData;
				if (!declareData.IsStatic) { break; } // only evaluate static declarations
				var rhs = EvaluateSlot(declareData.Value, analysis, programOutput, valueTable);
				valueTable.Add(declareData.Name, rhs);
				return rhs; // TODO: return l-value?
			}
		}
		return VoidEvaluation.Instance;
	}

	private static Object ObjFromEval(Evaluation eval)
	{
		return eval switch
		{
			IntEvaluation intExpressionResult => intExpressionResult.Value,
			StringEvaluation stringExpressionResult => stringExpressionResult.Value,
			_ => throw new NotImplementedException($"Invalid call argument: {eval}"),
		};
	}

	private static Evaluation EvalFromObj(Object obj)
	{
		return obj switch
		{
			int intResult => new IntEvaluation(intResult),
			string stringResult => new StringEvaluation(stringResult),
			_ => throw new NotImplementedException($"Invalid call argument: {obj}"),
		};
	}

	// TODO: remove unused code after migrating

	// private static Evaluation EvaluateParameterExpression(ParameterExpression parameterExpression, ValueTable table, StringWriter programOutput)
	// {
	// 	var parameter = table.GetNextParameter();
	// 	return parameter;
	// }

	// private static Evaluation EvaluateAssignExpression(AssignExpression assignExpression, ValueTable table, StringWriter programOutput)
	// {
	// 	// the only l-value we support is an identifier
	// 	if (assignExpression.Target is not IdentifierExpression identifierExpression)
	// 	{
	// 		throw new NotImplementedException($"Invalid assign expression target: {assignExpression.Target}");
	// 	}

	// 	var rhs = EvaluateTypedExpression(assignExpression.Value, table, programOutput);
	// 	table.Set(identifierExpression.Identifier, rhs);
	// 	return rhs; // TODO: return l-value?
	// }

	// private static Evaluation EvaluateCallDotnetMethodExpression(DotnetMemberMethodExpression callExpression, ValueTable table, StringWriter programOutput)
	// {
	// 	var target = EvaluateTypedExpression(callExpression.Target, table, programOutput);
	// 	return new DotnetMemberMethodEvaluation(MethodInfo: callExpression.MethodInfo, Target: target); // TODO: implement
	// }

	// private static Evaluation CallDotnetMemberMethodEvaluation(DotnetMemberMethodEvaluation callExpression, Evaluation[] evalArgs, ValueTable table, StringWriter programOutput)
	// {
	// 	if (callExpression is not { MethodInfo: { } methodInfo, Target: { } targetExpression })
	// 	{
	// 		throw new NotImplementedException($"Invalid call expression: {callExpression}");
	// 	}
	// 	var args = new Object[evalArgs.Length];
	// 	foreach (var (i, arg) in evalArgs.Index())
	// 	{
	// 		var argObj = EvaluateExpressionResult(arg);
	// 		args[i] = argObj;
	// 	}
	// 	var targetObject = EvaluateExpressionResult(targetExpression);
	// 	var returnValue = methodInfo.Invoke(targetObject, args);
	// 	var returnType = new DotnetAnalysisType(methodInfo.ReturnType);
	// 	var returnExpression = CreateExpressionResult(returnType, returnValue);
	// 	return returnExpression;
	// }
}
