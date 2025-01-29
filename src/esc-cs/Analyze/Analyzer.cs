namespace EscLang.Analyze;

using System.Reflection;
using EscLang.Parse;

// TODO: constant enforcement
// TODO: collect declarations before analyzing possible references
// TODO: combine call node handling across step/non-step methods

public static class Analyzer
{
	public static Analysis Analyze(Parse.EscFile file, TextWriter log)
	{
		var analysis = new Analysis();

		log.WriteLine("=== Build: init ===");
		var fileSlotId = BuildTable(file, 0, analysis, log);

		log.WriteLine("=== Build: return ===");
		BuildReturn(analysis, log);

		log.WriteLine("=== Build: resolve identifiers ===");
		BuildResolver(analysis, log);

		log.WriteLine("=== Build: types ===");
		BuildTypes(analysis, log);

		log.WriteLine("=== Tree ===");
		Printer.PrintTable(analysis, log);

		return analysis;
	}

	private static int BuildTable(SyntaxNode node, int parentSlot, Analysis analysis, TextWriter log)
	{
		switch (node)
		{
			case EscFile { Lines: { } lines }:
			{
				var fileData = new FileCodeData();
				var fileSlot = analysis.Add(parentSlot, CodeSlotEnum.File, fileData, log);

				var bracesData = new BracesCodeData(Lines: []);
				var bracesSlot = analysis.Add(fileSlot, CodeSlotEnum.Braces, bracesData, log);

				fileData = fileData with { Main = bracesSlot };
				analysis.UpdateData(fileSlot, fileData, log);

				// Lines
				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, bracesSlot, analysis, log);
					lineSlots.Add(lineSlot);
				}

				bracesData = bracesData with { Lines = [.. lineSlots] };
				analysis.UpdateData(bracesSlot, bracesData, log);

				return fileSlot;
			}
			case DeclareStaticNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(true, parentSlot, analysis, log, idNode, typeNode, valueNode);
			}
			case DeclareAssignNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(false, parentSlot, analysis, log, idNode, typeNode, valueNode);
			}
			case CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var data = new CallCodeData(Target: 0, Args: []);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Call, data, log);

				// Target
				var targetSlot = BuildTable(target, slot, analysis, log);
				data = data with { Target = targetSlot };
				analysis.UpdateData(slot, data, log);

				// Args
				var argSlots = new List<int>();
				foreach (var arg in arguments)
				{
					var argSlot = BuildTable(arg, slot, analysis, log);
					argSlots.Add(argSlot);
				}

				data = data with { Args = [.. argSlots] };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			case IdentifierNode { Text: { Length: > 0 } id }:
			{
				var data = new IdentifierCodeData(Name: id);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Identifier, data, log);

				return slot;
			}
			case BracesNode { Lines: { } lines }:
			{
				var data = new BracesCodeData([]);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Braces, data, log);

				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, slot, analysis, log);
					lineSlots.Add(lineSlot);
				}

				data = data with { Lines = [.. lineSlots] };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			case LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var data = new IntegerCodeData(Value: Int32.Parse(numberLiteral));
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Integer, data, log);

				return slot;
			}
			case LiteralStringNode { Text: { Length: > 0 } stringLiteral }:
			{
				var data = new StringCodeData(Value: stringLiteral);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.String, data, log);

				return slot;
			}
			case PlusNode { Left: { } left, Right: { } right }:
			{
				var data = new AddOpCodeData();
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Add, data, log);

				// Operands
				var leftSlot = BuildTable(left, slot, analysis, log);
				var rightSlot = BuildTable(right, slot, analysis, log);

				data = data with { Left = leftSlot, Right = rightSlot };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			case ParameterNode:
			{
				var data = new ParameterCodeData();
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Parameter, data, log);

				return slot;
			}
			case LogicalNegationNode { Node: { } innerNode }:
			{
				var innerSlotId = BuildTable(innerNode, parentSlot, analysis, log);
				var data = new LogicalNegationCodeData(Value: innerSlotId);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.LogicalNegation, data, log);

				return slot;
			}
			case AssignNode { Target: { } target, Value: { } value }:
			{
				var data = new AssignCodeData();
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Assign, data, log);

				// Target
				var targetSlot = BuildTable(target, slot, analysis, log);
				data = data with { Target = targetSlot };
				analysis.UpdateData(slot, data, log);

				// Value
				var valueSlot = BuildTable(value, slot, analysis, log);
				data = data with { Value = valueSlot };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			case MemberNode { Target: { } target, Member: { } member }:
			{
				var data = new MemberCodeData(0, 0, []);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Member, data, log);

				// Target
				var targetSlot = BuildTable(target, slot, analysis, log);
				data = data with { Target = targetSlot };
				analysis.UpdateData(slot, data, log);

				// Member
				var memberSlot = BuildTable(member, slot, analysis, log);
				data = data with { Member = memberSlot };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			default:
			{
				log.WriteLine($"unknown node for table: {node}");
				throw new NotImplementedException($"TODO: BuildTable: {node}");
			}
		}
	}

	private static void BuildReturn(Analysis analysis, TextWriter log)
	{
		var returnQueue = new Queue<int>();

		foreach (var (slot, node) in analysis.All.Index())
		{
			// all functions are procs unless a return statement is found
			if (node.CodeType == CodeSlotEnum.Braces)
			{
				var voidType = analysis.GetOrAddType(VoidTypeData.Instance, log);
				var retVoid = analysis.GetOrAddType(new FunctionTypeData(voidType), log);
				analysis.UpdateType(slot, retVoid, log);
				continue;
			}

			// void return
			if (node.CodeType == CodeSlotEnum.Identifier)
			{
				var idData = (IdentifierCodeData)node.Data;
				if (idData.Name == "return")
				{
					// TODO: shared void slot?
					var voidSlot = analysis.Add(slot, CodeSlotEnum.Void, VoidCodeData.Instance, log);					
					var voidType = analysis.GetOrAddType(VoidTypeData.Instance, log);
					analysis.UpdateType(voidSlot, voidType, log);

					var returnData = new ReturnCodeData(voidSlot);
					analysis.ReplaceData(slot, CodeSlotEnum.Return, returnData, log);
					analysis.UpdateType(slot, voidType, log);
					returnQueue.Enqueue(slot);
				}
				continue;
			}

			// call "return" id to return
			if (node.CodeType == CodeSlotEnum.Call)
			{
				var call = (CallCodeData)node.Data;
				if (call.Target == 0) { continue; }
				if (call.Args.Length != 1) { continue; }

				if (!analysis.TryGetSlot<IdentifierCodeData>(call.Target, CodeSlotEnum.Identifier, out var targetId, log))
				{
					continue;
				}
				if (targetId.Name != "return") { continue; }

				var argSlot = call.Args[0];
				if (argSlot == 0) { continue; }

				var returnData = new ReturnCodeData(argSlot);
				analysis.ReplaceData(slot, CodeSlotEnum.Return, returnData, log);
				returnQueue.Enqueue(slot);

				// Invalidate the "return" identifier
				analysis.ReplaceData(call.Target, CodeSlotEnum.Unknown, InvalidCodeData.Instance, log);

				log.WriteLine($"slot {slot:0000} in {node.ParentSlot:0000} -- call -> return");
			}
		}

		while (returnQueue.Count > 0)
		{
			var slot = returnQueue.Dequeue();
			var node = analysis.GetCodeSlot(slot);

			if (node.CodeType != CodeSlotEnum.Return) { continue; } // should be redundant

			var returnData = (ReturnCodeData)node.Data;
			if (returnData.Function != 0) { continue; }

			// recurse up to find the braces node
			var currentSlot = node.ParentSlot;
			while (true)
			{
				// not embedded in a braces node?
				if (currentSlot == 0)
				{
					throw new InvalidOperationException("Return statement not in a braces node");
					break;
				}

				var currentNode = analysis.GetCodeSlot(currentSlot);
				if (currentNode.CodeType == CodeSlotEnum.Braces)
				{
					var bracesData = (BracesCodeData)currentNode.Data;

					log.WriteLine($"slot {slot:0000} <- returns to {currentSlot:0000}");

					analysis.UpdateData(slot, returnData with { Function = currentSlot }, log);

					// clear "proc" type since we found a return statement
					analysis.UpdateType(currentSlot, 0, log);
					break;
				}

				currentSlot = currentNode.ParentSlot;
			}
		}
	}

	private static void BuildResolver(Analysis analysis, TextWriter log)
	{
		foreach (var (slot, node) in analysis.All.Index())
		{
			if (node.CodeType != CodeSlotEnum.Identifier) { continue; }
			var slotData = (IdentifierCodeData)node.Data;
			log.WriteLine($"identifier: {slot:0000} = {slotData}");
			var ident = slotData.Name;

			var indent = 0;
			var currentNode = node;
			while (true)
			{
				indent++;
				if (indent > 100) { throw new InvalidOperationException("Infinite loop"); }

				var currentSlot = currentNode.ParentSlot;
				if (currentSlot == 0)
				{
					// check for intrinsics
					if (ident == "true")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Boolean, new BooleanCodeData(true), log);
						var fun = analysis.GetOrAddType(new NativeTypeData("bool"), log);
						analysis.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "false")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Boolean, new BooleanCodeData(false), log);
						var fun = analysis.GetOrAddType(new NativeTypeData("bool"), log);
						analysis.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "print")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Intrinsic, new IntrinsicCodeData("print"), log);
						var str = analysis.GetOrAddType(new NativeTypeData("string"), log);
						var fun = analysis.GetOrAddType(new FunctionTypeData(str), log);
						analysis.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "int")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Intrinsic, new IntrinsicCodeData("int"), log);
						var fun = analysis.GetOrAddType(new NativeTypeData("int"), log);
						var meta = analysis.GetOrAddType(new MetaTypeData(fun), log);
						analysis.UpdateType(slot, meta, log);
						break;
					}
					if (ident == "bool")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Intrinsic, new IntrinsicCodeData("bool"), log);
						var fun = analysis.GetOrAddType(new NativeTypeData("bool"), log);
						var meta = analysis.GetOrAddType(new MetaTypeData(fun), log);
						analysis.UpdateType(slot, meta, log);
						break;
					}
					if (ident == "if")
					{
						var callNode = analysis.GetCodeSlot(node.ParentSlot);
						var callData = analysis.GetCodeData<CallCodeData>(node.ParentSlot);
						if (callData.Args.Length != 2)
						{
							throw new InvalidOperationException("Invalid if statement");
						}
						var ifData = new IfSlotCodeData(Condition: callData.Args[0], Body: callData.Args[1]);
						// var ifSlot = table.Add(node.ParentSlot, TableSlotType.If, ifData, log);
						analysis.ReplaceData(node.ParentSlot, CodeSlotEnum.If, ifData, log);
						analysis.ReplaceData(slot, CodeSlotEnum.Unknown, InvalidCodeData.Instance, log); // invalidate id slot
						break;
					}

					log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}0000 = ROOT");
					break;
				}
				currentNode = analysis.GetCodeSlot(currentSlot);

				if (currentNode.CodeType == CodeSlotEnum.Braces)
				{
					var bracesData = (BracesCodeData)currentNode.Data;
					if (bracesData.TryGetNameTableValue(ident, out var valueSlot))
					{
						log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}{currentSlot:0000} FOUND: {bracesData}");
						var newData = slotData with { Target = valueSlot };
						analysis.UpdateData(slot, newData, log);
						break;
					}
				}
				else
				{
					log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}{currentSlot:0000} = {currentNode.CodeType}");

				}
			}
		}
	}

	private static void BuildTypes(Analysis analysis, TextWriter log)
	{
		// updated nodes that trigger its dependents to refresh
		var sourceQueue = new Queue<int>();

		foreach (var (slot, node) in analysis.All.Index())
		{
			switch (node.CodeType)
			{
				case CodeSlotEnum.Void:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.Integer:
				{
					var intType = analysis.GetOrAddType(new NativeTypeData("int"), log);
					analysis.UpdateType(slot, intType, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.String:
				{
					var strType = analysis.GetOrAddType(new NativeTypeData("string"), log);
					analysis.UpdateType(slot, strType, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.Intrinsic:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					log.WriteLine($"intrinsic: {slot:0000} = {node}");
					// TODO: intrinsic types
					break;
				}
				case CodeSlotEnum.Parameter:
				{
					var intType = analysis.GetOrAddType(ParameterTypeData.Instance, log);
					analysis.UpdateType(slot, intType, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
			}
		}

		var iteration = 0;
		while (sourceQueue.Count > 0)
		{
			if (++iteration > 1000) { throw new InvalidOperationException("Type checking reached max iterations"); }

			var sourceSlotId = sourceQueue.Dequeue();
			log.WriteLine($"dequeue: {sourceSlotId:0000}");

			// find all nodes that reference this node
			// future: build a reverse lookup table on an earlier pass
			// note two targets to be refreshed with each update: 1) parent, 2) arbitrary references
			foreach (var (targetSlotId, targetSlot) in analysis.All.Index())
			{
				if (targetSlot.CodeType == CodeSlotEnum.Declare)
				{
					var declareData = analysis.GetCodeData<DeclareCodeData>(targetSlotId);

					if (targetSlot.TypeSlot != 0) { continue; } // already set

					if (declareData.Type == sourceSlotId)
					{
						var s2 = analysis.GetCodeSlot(declareData.Type);
						var t2 = analysis.GetCodeData<IntrinsicCodeData>(declareData.Type);

						var t3 = analysis.GetTypeData(s2.TypeSlot);
						if (t3 is MetaTypeData { InstanceType: { } instanceType2 })
						{
							analysis.UpdateType(targetSlotId, instanceType2, log);
							sourceQueue.Enqueue(targetSlotId);
						}
						else
						{
							throw new NotImplementedException($"Invalid type: {t3}");
						}
						continue;
					}

					if (declareData.Value == sourceSlotId)
					{
						// targetQueue.Enqueue(targetSlotId);
						log.WriteLine($"add target: {targetSlotId}");
						if (declareData.Type != 0)
						{
							// TODO: explicit types
							break;
						}
						else
						{
							var valueSlotId = declareData.Value;
							var valueSlot = analysis.GetCodeSlot(valueSlotId);
							var valueSlotType = valueSlot.TypeSlot;

							if (valueSlotType != targetSlot.TypeSlot)
							{
								analysis.UpdateType(targetSlotId, valueSlotType, log);
								sourceQueue.Enqueue(targetSlotId);
							}
						}
					}
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Add)
				{
					// NOTE: this assumes that add produces the same type as its operands

					var addSlot = analysis.GetCodeSlot(targetSlotId);
					var addData = analysis.GetCodeData<AddOpCodeData>(targetSlotId);

					// skip if either operand is not known yet
					if (addData.Left == 0 || addData.Right == 0)
					{
						continue;
					}

					var leftType = analysis.GetCodeSlot(addData.Left).TypeSlot;
					var rightType = analysis.GetCodeSlot(addData.Right).TypeSlot;

					// skip if operands do not match
					if (leftType != rightType)
					{
						continue;
					}

					// skip if already set
					if (leftType == addSlot.TypeSlot)
					{
						continue;
					}

					analysis.UpdateType(targetSlotId, leftType, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Identifier)
				{
					var idSlot = analysis.GetCodeSlot(targetSlotId);
					var idData = analysis.GetCodeData<IdentifierCodeData>(targetSlotId);

					if (idData.Target != sourceSlotId)
					{
						continue;
					}

					log.WriteLine($"id target: {targetSlotId}");

					var sourceSlotRecord = analysis.GetCodeSlot(sourceSlotId);
					if (sourceSlotRecord.TypeSlot == 0) { continue; } // should be redundant

					var typeSlot = analysis.GetTypeData(sourceSlotRecord.TypeSlot);
					if (typeSlot is FunctionTypeData)
					{
						// identifier to a function must turn into a call
						var parentSlot = analysis.GetCodeSlot(idSlot.ParentSlot);
						if (parentSlot.CodeType != CodeSlotEnum.Call)
						{
							var newIdSlotId = analysis.Add(targetSlotId, CodeSlotEnum.Identifier, idData, log);
							analysis.UpdateType(newIdSlotId, sourceSlotRecord.TypeSlot, log);
							analysis.ReplaceData(targetSlotId, CodeSlotEnum.Call, new CallCodeData(Target: newIdSlotId, Args: []), log);
							sourceQueue.Enqueue(newIdSlotId);
							sourceQueue.Enqueue(targetSlotId);
							continue;
						}
					}

					analysis.UpdateType(targetSlotId, sourceSlotRecord.TypeSlot, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Return)
				{
					var returnSlot = analysis.GetCodeSlot(targetSlotId);
					var returnData = analysis.GetCodeData<ReturnCodeData>(targetSlotId);

					if (returnData.Value != sourceSlotId)
					{
						continue;
					}

					var sourceTypeId = analysis.GetCodeSlot(sourceSlotId).TypeSlot;
					var typeRow = analysis.GetTypeData(sourceTypeId);
					log.WriteLine($"slot {targetSlotId:0000} return: type <- {typeRow} {sourceTypeId} via {returnData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Braces)
				{
					var bracesSlot = analysis.GetCodeSlot(targetSlotId);
					var bracesData = analysis.GetCodeData<BracesCodeData>(targetSlotId);

					if (bracesSlot.TypeSlot != 0) { continue; } // already found a return value, but might need to consider more?

					var returnSlot = analysis.GetCodeSlot(sourceSlotId);
					if (returnSlot.CodeType != CodeSlotEnum.Return) { continue; } // should be redundant
					var returnData = (ReturnCodeData)returnSlot.Data;
					if (returnData.Function != targetSlotId) { continue; } // should be redundant
					if (returnSlot.TypeSlot == 0) { continue; } // should be redundant

					var returnType = analysis.GetTypeData(returnSlot.TypeSlot);
					var funcType = new FunctionTypeData(returnSlot.TypeSlot);
					var funcTypeId = analysis.GetOrAddType(funcType, log);

					log.WriteLine($"slot {targetSlotId:0000} braces: found return {sourceSlotId:0000} {funcTypeId}");
					analysis.UpdateType(targetSlotId, funcTypeId, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Call)
				{
					var callSlot = analysis.GetCodeSlot(targetSlotId);
					var callData = analysis.GetCodeData<CallCodeData>(targetSlotId);
					if (callSlot.TypeSlot != 0) { continue; } // already set

					// match dequeued source slot
					if (callData.Target != sourceSlotId && !callData.Args.Contains(sourceSlotId)) { continue; }
					var callTargetSlot = analysis.GetCodeSlot(callData.Target);

					// skip if target or arg types are not known yet
					if (callTargetSlot.TypeSlot == 0) { continue; }
					if (callData.Args.Any(i => analysis.GetCodeSlot(i).TypeSlot == 0)) { continue; }

					var callTargetType = analysis.GetTypeData(callTargetSlot.TypeSlot);
					if (callTargetType is FunctionTypeData { ReturnType: var returnType })
					{
						analysis.UpdateType(targetSlotId, returnType, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else if (callTargetType is MemberTypeData { TargetType: var targetType })
					{
						// TODO: update target type from Member to Method
						var memberSlot = analysis.GetCodeSlot(callData.Target);
						var memberData = analysis.GetCodeData<MemberCodeData>(callData.Target);

						MethodInfo? found = null;
						foreach (var memberInfo in memberData.Members)
						{
							if (memberInfo is not MethodInfo methodInfo) { continue; }
							if (methodInfo.GetParameters().Length != callData.Args.Length) { continue; }

							// TODO: check arg types
							found = methodInfo;
						}

						if (found is null) { throw new InvalidOperationException("Invalid member call"); }

						var returnDotNetType = new DotnetTypeData(found.ReturnType);
						var returnDotNetTypeId = analysis.GetOrAddType(returnDotNetType, log);

						analysis.UpdateType(targetSlotId, returnDotNetTypeId, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else { throw new InvalidOperationException($"Invalid call target type: {callTargetType}"); }
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Assign)
				{
					var assignSlot = analysis.GetCodeSlot(targetSlotId);
					var assignData = analysis.GetCodeData<AssignCodeData>(targetSlotId);

					if (assignData.Value != sourceSlotId) { continue; }

					var sourceTypeId = analysis.GetCodeSlot(sourceSlotId).TypeSlot;
					var typeRow = analysis.GetTypeData(sourceTypeId);
					log.WriteLine($"slot {targetSlotId:0000} assign: type <- {typeRow} {sourceTypeId} via {assignData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Member)
				{
					var memberSlot = analysis.GetCodeSlot(targetSlotId);
					var memberData = analysis.GetCodeData<MemberCodeData>(targetSlotId);

					// NOTE: right side will not have a type because it depends on the left side

					if (memberData.Target != sourceSlotId && memberData.Member != sourceSlotId) { continue; }

					var leftSlot = analysis.GetCodeSlot(memberData.Target);
					var rightSlot = analysis.GetCodeSlot(memberData.Member);

					if (leftSlot.TypeSlot == 0) { continue; }
					var leftType = analysis.GetTypeData(leftSlot.TypeSlot);

					if (rightSlot.CodeType != CodeSlotEnum.Identifier) { throw new InvalidOperationException($"Invalid member type: {rightSlot.CodeType}"); }
					var rightData = analysis.GetCodeData<IdentifierCodeData>(memberData.Member);
					var rightName = rightData.Name;

					if (leftType is NativeTypeData { Name: { } nativeTypeName })
					{
						// convert keywords to full type name
						nativeTypeName = nativeTypeName switch
						{
							"int" => "System.Int32",
							"bool" => "System.Boolean",
							"string" => "System.String",
							_ => nativeTypeName,
						};

						if (Type.GetType(nativeTypeName) is not { } dotnetType)
						{
							throw new InvalidOperationException($"Invalid native type: {nativeTypeName}");
						}
						var members = dotnetType.GetMember(rightName);
						if (members.Length == 0)
						{
							throw new InvalidOperationException($"Invalid member: {rightName}");
						}

						var memberCodeData = memberData with { Members = members };
						analysis.UpdateData(targetSlotId, memberCodeData, log);

						var ttt = new MemberTypeData(leftSlot.TypeSlot);
						var tttt = analysis.GetOrAddType(ttt, log);
						analysis.UpdateType(targetSlotId, tttt, log);
						sourceQueue.Enqueue(targetSlotId);
						continue;
					}
					else
					{
						throw new NotImplementedException($"Invalid type: {leftType}");
					}
				}
				else
				{
					continue;
				}
			}
		}
		log.WriteLine($"done after {iteration} iterations");
	}

	private static int BuildDeclareNode(Boolean isStatic, int parentSlot, Analysis analysis, TextWriter log, SyntaxNode idNode, SyntaxNode? typeNode, SyntaxNode valueNode)
	{
		if (idNode is not Parse.IdentifierNode { Text: { Length: > 0 } id })
		{
			throw new Exception("Invalid identifier");
		}

		// TODO: add slot, enqueue child nodes, queue update slot with analyzed data
		var data = new DeclareCodeData(Name: id, IsStatic: isStatic);
		var slot = analysis.Add(parentSlot, CodeSlotEnum.Declare, data, log);

		// Name
		if (!analysis.TryGetSlot<BracesCodeData>(parentSlot, CodeSlotEnum.Braces, out var bracesData, log))
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
			typeSlot = BuildTable(typeNode, slot, analysis, log);
			data = data with { Type = typeSlot };
			analysis.UpdateData(slot, data, log);
		}

		// Value
		var valueSlot = BuildTable(valueNode, slot, analysis, log);
		data = data with { Value = valueSlot };
		analysis.UpdateData(slot, data, log);

		return slot;
	}
}