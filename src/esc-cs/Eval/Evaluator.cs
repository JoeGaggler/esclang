using EscLang.Analyze;

namespace EscLang.Eval;

// TODO: out of order function declarations
// TODO: programOutput is only needed for print statements, should be removed from method signatures

public static class Evaluator
{
	public static Evaluation Evaluate(Table slotTable, StringWriter programOutput)
	{
		var globalTable = new ValueTable();

		var slotId = 1; // TODO: get braces slot id from table
		return EvaluateSlot(slotId, slotTable, programOutput, globalTable);
	}

	private static Evaluation CallFunctionSlot(int bracesSlotId, Evaluation[] args, Table slotTable, StringWriter programOutput, ValueTable valueTable)
	{
		var (bracesSlot, bracesData) = slotTable.GetSlotTuple<BracesSlotData>(bracesSlotId);

		var bracesValueTable = new ValueTable(valueTable);
		bracesValueTable.SetArguments(args);

		foreach (var step in bracesData.Lines)
		{
			var stepNode = EvaluateSlot(step, slotTable, programOutput, bracesValueTable);
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

	private static Evaluation EvaluateSlot(int slotId, Table slotTable, StringWriter programOutput, ValueTable valueTable)
	{
		var (slot, slotData) = slotTable.GetSlotTuple<SlotData>(slotId);
		switch (slot.DataType)
		{
			case TableSlotType.Integer: return new IntEvaluation(((Analyze.IntegerSlotData)slotData).Value);
			case TableSlotType.String: return new StringEvaluation(((Analyze.StringSlotData)slotData).Value);

			case TableSlotType.File:
			{
				var fileData = (Analyze.FileSlotData)slotData;
				var mainDeclResult = EvaluateSlot(fileData.Main, slotTable, programOutput, valueTable);
				if (mainDeclResult is not FunctionEvaluation2 { BracesSlotId: { } bracesSlotId })
				{
					throw new NotImplementedException($"Invalid main declaration: {mainDeclResult}");
				}

				return CallFunctionSlot(bracesSlotId, [], slotTable, programOutput, valueTable);
			}
			case TableSlotType.Braces:
			{
				// var braceData = (Analyze.BracesSlotData)slotData;
				return new FunctionEvaluation2(slotId);
			}
			case TableSlotType.Declare:
			{
				var declareData = (Analyze.DeclareSlotData)slotData;
				var rhs = EvaluateSlot(declareData.Value, slotTable, programOutput, valueTable);
				valueTable.Add(declareData.Name, rhs);
				return rhs; // TODO: return l-value?
			}
			case TableSlotType.Return:
			{
				var returnData = (Analyze.ReturnSlotData)slotData;
				var returnValue = EvaluateSlot(returnData.Value, slotTable, programOutput, valueTable);
				return new ReturnValueEvaluation(returnValue);
			}
			case TableSlotType.Identifier:
			{
				var identifierData = (Analyze.IdentifierSlotData)slotData;
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
			case TableSlotType.Add:
			{
				var addData = (Analyze.AddOpSlotData)slotData;
				var left = EvaluateSlot(addData.Left, slotTable, programOutput, valueTable);
				var right = EvaluateSlot(addData.Right, slotTable, programOutput, valueTable);

				var intTypeSlotId = slotTable.GetOrAddType(new NativeTypeSlot("int"), StreamWriter.Null);
				if (slot.TypeSlot != intTypeSlotId)
				{
					throw new NotImplementedException($"Invalid add expression type: {slot.TypeSlot}");
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
			case TableSlotType.Intrinsic:
			{
				var intrinsicData = (Analyze.IntrinsicSlotData)slotData;
				return new IntrinsicFunctionEvaluation(intrinsicData.Name);
			}
			case TableSlotType.Parameter:
			{
				var parameterData = (Analyze.ParameterSlotData)slotData;
				var parameter = valueTable.GetNextParameter();
				return parameter;
			}
			case TableSlotType.Call:
			{
				var callData = (Analyze.CallSlotData)slotData;
				var args = new Evaluation[callData.Args.Length];
				foreach (var (i, arg) in callData.Args.Index())
				{
					var argExp = EvaluateSlot(arg, slotTable, programOutput, valueTable);
					args[i] = argExp;
				}
				var targetExpression = EvaluateSlot(callData.Target, slotTable, programOutput, valueTable);
				if (false) { }
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
						// case "if":
						// {
						// 	var condition = args[0];
						// 	var ifBlock = callData.Args[1];
						// 	if (condition is not BooleanEvaluation booleanExpressionResult)
						// 	{
						// 		throw new NotImplementedException($"Invalid if condition: {condition}");
						// 	}
						// 	if (!booleanExpressionResult.Value)
						// 	{
						// 		return VoidEvaluation.Instance;
						// 	}
						// 	if (ifBlock is not FunctionExpression functionScopeExpression)
						// 	{
						// 		throw new NotImplementedException($"Invalid if block: {ifBlock}");
						// 	}
						// 	return EvaluateSlot(ifBlock, slotTable, programOutput, valueTable);
						// }
						default:
						{
							throw new NotImplementedException($"Invalid intrinsic function: {intrinsic}");
						}
					}
				}
				else if (targetExpression is FunctionEvaluation2 { BracesSlotId: { } bracesSlotId })
				{
					// TODO: ARGS!
					var returnExpression = CallFunctionSlot(bracesSlotId, args, slotTable, programOutput, valueTable);
					return returnExpression;
				}
				// else if (targetExpression is DotnetMemberMethodEvaluation { } eval)
				// {
				// 	var returnExpression = CallDotnetMemberMethodEvaluation(eval, args, slotTable, programOutput, valueTable);
				// 	return returnExpression;
				// }
				else
				{
					throw new NotImplementedException($"Invalid call target: {targetExpression}");
				}
			}
			default:
			{
				throw new InvalidOperationException($"Invalid slot type: {slot}");
			}
		}
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
