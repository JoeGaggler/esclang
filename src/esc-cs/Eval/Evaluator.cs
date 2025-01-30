using System.Reflection;
using EscLang.Analyze;

namespace EscLang.Eval;

// TODO: out of order function declarations
// TODO: programOutput is only needed for print statements, should be removed from method signatures

public static class Evaluator
{
	public static Evaluation Evaluate(Analysis slotTable, StringWriter programOutput)
	{
		var globalTable = new ValueTable();

		var slotId = 1; // TODO: get braces slot id from table
		return EvaluateSlot(slotId, slotTable, programOutput, globalTable);
	}

	private static Evaluation CallFunctionSlot(int bracesSlotId, Evaluation[] args, Analysis slotTable, StringWriter programOutput, ValueTable valueTable)
	{
		var bracesSlot = slotTable.GetCodeSlot(bracesSlotId);
		var bracesData = slotTable.GetCodeData<BracesCodeData>(bracesSlotId);

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

	private static Evaluation CallInlineSlot(int bracesSlotId, Evaluation[] args, Analysis slotTable, StringWriter programOutput, ValueTable valueTable)
	{
		var bracesSlot = slotTable.GetCodeSlot(bracesSlotId);
		var bracesData = slotTable.GetCodeData<BracesCodeData>(bracesSlotId);

		var bracesValueTable = new ValueTable(valueTable);
		bracesValueTable.SetArguments(args);

		foreach (var step in bracesData.Lines)
		{
			var stepNode = EvaluateSlot(step, slotTable, programOutput, bracesValueTable);
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

	private static Evaluation EvaluateSlot(int slotId, Analysis slotTable, StringWriter programOutput, ValueTable valueTable)
	{
		var slot = slotTable.GetCodeSlot(slotId);
		var slotData = slotTable.GetCodeData<CodeData>(slotId);
		switch (slot.CodeType)
		{
			case CodeSlotEnum.Boolean: return new BooleanEvaluation(((Analyze.BooleanCodeData)slotData).Value);
			case CodeSlotEnum.Integer: return new IntEvaluation(((Analyze.IntegerCodeData)slotData).Value);
			case CodeSlotEnum.String: return new StringEvaluation(((Analyze.StringCodeData)slotData).Value);

			case CodeSlotEnum.File:
			{
				var fileData = (Analyze.FileCodeData)slotData;
				var mainDeclResult = EvaluateSlot(fileData.Main, slotTable, programOutput, valueTable);
				if (mainDeclResult is not FunctionEvaluation { BracesSlotId: { } bracesSlotId })
				{
					throw new NotImplementedException($"Invalid main declaration: {mainDeclResult}");
				}

				return CallFunctionSlot(bracesSlotId, [], slotTable, programOutput, valueTable);
			}
			case CodeSlotEnum.Braces:
			{
				// var braceData = (Analyze.BracesSlotData)slotData;
				return new FunctionEvaluation(slotId);
			}
			case CodeSlotEnum.Declare:
			{
				var declareData = (Analyze.DeclareCodeData)slotData;
				var rhs = EvaluateSlot(declareData.Value, slotTable, programOutput, valueTable);
				valueTable.Add(declareData.Name, rhs);
				return rhs; // TODO: return l-value?
			}
			case CodeSlotEnum.Return:
			{
				var returnData = (Analyze.ReturnCodeData)slotData;
				if (returnData.Value == 0)
				{
					return ReturnVoidEvaluation.Instance;
				}
				var returnValue = EvaluateSlot(returnData.Value, slotTable, programOutput, valueTable);
				return new ReturnValueEvaluation(returnValue);
			}
			case CodeSlotEnum.Identifier:
			{
				var identifierData = (Analyze.IdentifierCodeData)slotData;
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
			case CodeSlotEnum.Assign:
			{
				var assignData = (Analyze.AssignCodeData)slotData;
				var rhs = EvaluateSlot(assignData.Value, slotTable, programOutput, valueTable);

				var idSlot = slotTable.GetCodeSlot(assignData.Target);
				var idData = slotTable.GetCodeData<IdentifierCodeData>(assignData.Target);
				var id = idData.Name;

				valueTable.Set(id, rhs);
				return rhs; // TODO: return l-value?
			}
			case CodeSlotEnum.Add:
			{
				var addData = (Analyze.AddOpCodeData)slotData;
				var left = EvaluateSlot(addData.Left, slotTable, programOutput, valueTable);
				var right = EvaluateSlot(addData.Right, slotTable, programOutput, valueTable);

				var intTypeSlotId = slotTable.GetOrAddType(new DotnetTypeData(typeof(int)), StreamWriter.Null);
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
			case CodeSlotEnum.Intrinsic:
			{
				var intrinsicData = (Analyze.IntrinsicCodeData)slotData;
				return new IntrinsicFunctionEvaluation(intrinsicData.Name);
			}
			case CodeSlotEnum.Parameter:
			{
				var parameterData = (Analyze.ParameterCodeData)slotData;
				var parameter = valueTable.GetNextParameter();
				return parameter;
			}
			case CodeSlotEnum.LogicalNegation:
			{
				var logicalNegationData = (Analyze.LogicalNegationCodeData)slotData;
				var value = EvaluateSlot(logicalNegationData.Value, slotTable, programOutput, valueTable);
				if (value is not BooleanEvaluation booleanExpressionResult)
				{
					throw new NotImplementedException($"Invalid logical negation value: {value}");
				}
				return new BooleanEvaluation(!booleanExpressionResult.Value);
			}
			case CodeSlotEnum.Call:
			{
				var callData = (Analyze.CallCodeData)slotData;
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
				else if (targetExpression is FunctionEvaluation { BracesSlotId: { } bracesSlotId })
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
				else if (targetExpression is MemberEvaluation { Target: { } target, Name: { } memberName, Member: { } memberInfo } TODO)
				{
					// TODO: HARD-CODED FOR CURRENT PROGRAM.ESC FILE
					var methodInfo = typeof(int).GetMethod("ToString", []);
					// if (callExpression is not { MethodInfo: { } methodInfo, Target: { } targetExpression })
					// {
					// 	throw new NotImplementedException($"Invalid call expression: {callExpression}");
					// }
					// var args = new Object[evalArgs.Length];
					// foreach (var (i, arg) in evalArgs.Index())
					// {
					// 	var argObj = EvaluateExpressionResult(arg);
					// 	args[i] = argObj;
					// }

					// var targetObject = EvaluateExpressionResult(targetExpression);
					var targetObject = 10045;

					args = []; // TODO: REMOVE THIS
					var returnValue = methodInfo.Invoke(targetObject, args);
					// var returnType = new DotnetAnalysisType(methodInfo.ReturnType);

					// var returnExpression = CreateExpressionResult(returnType, returnValue);
					var returnExpression = new StringEvaluation("10045");
					
					return returnExpression;
					throw new NotImplementedException($"TODO: call member: {TODO}");
				}
				else
				{
					throw new NotImplementedException($"Invalid call target: {targetExpression}");
				}
			}
			case CodeSlotEnum.If:
			{
				var ifData = (Analyze.IfSlotCodeData)slotData;
				var condition = EvaluateSlot(ifData.Condition, slotTable, programOutput, valueTable);
				if (condition is not BooleanEvaluation booleanExpressionResult)
				{
					throw new NotImplementedException($"Invalid if condition: {condition}");
				}
				if (!booleanExpressionResult.Value)
				{
					return VoidEvaluation.Instance;
				}
				var ifBodyResult = CallInlineSlot(ifData.Body, [], slotTable, programOutput, valueTable);
				return ifBodyResult;
			}
			case CodeSlotEnum.Member:
			{
				var memberData = (Analyze.MemberCodeData)slotData;
				var memberName = slotTable.GetCodeData<IdentifierCodeData>(memberData.Member).Name;
				var target = EvaluateSlot(memberData.Target, slotTable, programOutput, valueTable);
				return new MemberEvaluation(target, memberName, memberData.Members);
			}
			case CodeSlotEnum.Void:
			{
				return VoidEvaluation.Instance;
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
