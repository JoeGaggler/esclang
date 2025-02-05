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
				var fileData = new FileSlotData();
				var fileSlot = analysis.Add(parentSlot, SlotEnum.File, fileData, log);

				var bracesData = new BracesSlotData(Lines: []);
				var bracesSlot = analysis.Add(fileSlot, SlotEnum.Braces, bracesData, log);

				// type
				var typeSlot = analysis.GetOrAddType(MetaTypeSlotData.Root, log);
				analysis.UpdateType(typeSlot, typeSlot, log); // type is a type!
				bracesData.TryAddNameTableValue("type", typeSlot);

				// int
				var intSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(int)), log);
				bracesData.TryAddNameTableValue("int", intSlot);

				// bool
				var boolSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(bool)), log);
				bracesData.TryAddNameTableValue("bool", boolSlot);

				// string
				var stringSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(string)), log);
				bracesData.TryAddNameTableValue("string", stringSlot);

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
				var data = new CallSlotData(Target: 0, Args: []);
				var slot = analysis.Add(parentSlot, SlotEnum.Call, data, log);

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
				var data = new IdentifierSlotData(Name: id);
				var slot = analysis.Add(parentSlot, SlotEnum.Identifier, data, log);

				return slot;
			}
			case BracesNode { Lines: { } lines }:
			{
				var data = new BracesSlotData([]);
				var slot = analysis.Add(parentSlot, SlotEnum.Braces, data, log);

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
				var data = new IntegerSlotData(Value: Int32.Parse(numberLiteral));
				var slot = analysis.Add(parentSlot, SlotEnum.Integer, data, log);

				return slot;
			}
			case LiteralStringNode { Text: { Length: > 0 } stringLiteral }:
			{
				var data = new StringSlotData(Value: stringLiteral);
				var slot = analysis.Add(parentSlot, SlotEnum.String, data, log);

				return slot;
			}
			case PlusNode { Left: { } left, Right: { } right }:
			{
				var data = new AddSlotData();
				var slot = analysis.Add(parentSlot, SlotEnum.Add, data, log);

				// Operands
				var leftSlot = BuildTable(left, slot, analysis, log);
				var rightSlot = BuildTable(right, slot, analysis, log);

				data = data with { Left = leftSlot, Right = rightSlot };
				analysis.UpdateData(slot, data, log);

				return slot;
			}
			case ParameterNode:
			{
				var data = new ParameterSlotData();
				var slot = analysis.Add(parentSlot, SlotEnum.Parameter, data, log);

				return slot;
			}
			case LogicalNegationNode { Node: { } innerNode }:
			{
				var innerSlotId = BuildTable(innerNode, parentSlot, analysis, log);
				var data = new LogicalNegationSlotData(Value: innerSlotId);
				var slot = analysis.Add(parentSlot, SlotEnum.LogicalNegation, data, log);

				return slot;
			}
			case NegationNode { Node: { } innerNode }:
			{
				var innerSlotId = BuildTable(innerNode, parentSlot, analysis, log);
				var data = new NegationSlotData(Value: innerSlotId);
				var slot = analysis.Add(parentSlot, SlotEnum.Negation, data, log);

				return slot;
			}
			case AssignNode { Target: { } target, Value: { } value }:
			{
				var data = new AssignSlotData();
				var slot = analysis.Add(parentSlot, SlotEnum.Assign, data, log);

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
				var data = new MemberSlotData(0, 0);
				var slot = analysis.Add(parentSlot, SlotEnum.Member, data, log);

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
			if (node.CodeType == SlotEnum.Braces)
			{
				var voidSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(void)), log);
				analysis.UpdateType(slot, voidSlot, log);
				continue;
			}

			// void return
			if (node.CodeType == SlotEnum.Identifier)
			{
				var idData = (IdentifierSlotData)node.Data;
				if (idData.Name == "return")
				{
					// TODO: shared void value slot?
					var voidType = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(void)), log);
					var voidSlot = analysis.Add(slot, SlotEnum.Void, VoidSlotData.Instance, log);
					analysis.UpdateType(voidSlot, voidType, log);

					var returnData = new ReturnSlotData(voidSlot);
					analysis.ReplaceData(slot, SlotEnum.Return, returnData, log);
					analysis.UpdateType(slot, voidType, log);
					returnQueue.Enqueue(slot);
				}
				continue;
			}

			// call "return" id to return
			if (node.CodeType == SlotEnum.Call)
			{
				var call = (CallSlotData)node.Data;
				if (call.Target == 0) { continue; }
				if (call.Args.Length != 1) { continue; }

				if (!analysis.TryGetSlot<IdentifierSlotData>(call.Target, SlotEnum.Identifier, out var targetId, log))
				{
					continue;
				}
				if (targetId.Name != "return") { continue; }

				var argSlot = call.Args[0];
				if (argSlot == 0) { continue; }

				var returnData = new ReturnSlotData(argSlot);
				analysis.ReplaceData(slot, SlotEnum.Return, returnData, log);
				returnQueue.Enqueue(slot);

				// Invalidate the "return" identifier
				analysis.ReplaceData(call.Target, SlotEnum.Unknown, InvalidSlotData.Instance, log);

				log.WriteLine($"slot {slot:0000} in {node.Parent:0000} -- call -> return");
			}
		}

		while (returnQueue.Count > 0)
		{
			var slot = returnQueue.Dequeue();
			var node = analysis.GetSlot(slot);

			if (node.CodeType != SlotEnum.Return) { continue; } // should be redundant

			var returnData = (ReturnSlotData)node.Data;
			if (returnData.Function != 0) { continue; }

			// recurse up to find the braces node
			var currentSlot = node.Parent;
			while (true)
			{
				// not embedded in a braces node?
				if (currentSlot == 0)
				{
					throw new InvalidOperationException("Return statement not in a braces node");
				}

				var currentNode = analysis.GetSlot(currentSlot);
				if (currentNode.CodeType == SlotEnum.Braces)
				{
					var bracesData = (BracesSlotData)currentNode.Data;

					log.WriteLine($"slot {slot:0000} <- returns to {currentSlot:0000}");

					analysis.UpdateData(slot, returnData with { Function = currentSlot }, log);

					// clear "proc" type since we found a return statement
					analysis.UpdateType(currentSlot, 0, log);
					break;
				}

				currentSlot = currentNode.Parent;
			}
		}
	}

	private static void BuildResolver(Analysis analysis, TextWriter log)
	{
		foreach (var (slot, node) in analysis.All.Index())
		{
			if (node.CodeType != SlotEnum.Identifier) { continue; }
			var slotData = (IdentifierSlotData)node.Data;
			log.WriteLine($"identifier: {slot:0000} = {slotData}");
			var ident = slotData.Name;

			var indent = 0;
			var currentNode = node;
			while (true)
			{
				indent++;
				if (indent > 100) { throw new InvalidOperationException("Infinite loop"); }

				var currentSlot = currentNode.Parent;
				if (currentSlot == 0)
				{
					// check for intrinsics
					if (ident == "true")
					{
						var boolSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(bool)), log);
						analysis.ReplaceData(slot, SlotEnum.Boolean, new BooleanSlotData(true), log);
						analysis.UpdateType(slot, boolSlot, log);
						break;
					}
					if (ident == "false")
					{
						var boolSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(bool)), log);
						analysis.ReplaceData(slot, SlotEnum.Boolean, new BooleanSlotData(false), log);
						analysis.UpdateType(slot, boolSlot, log);
						break;
					}
					if (ident == "print")
					{
						analysis.ReplaceData(slot, SlotEnum.Intrinsic, new IntrinsicSlotData("print"), log);
						var fff1 = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(string)), log);
						var fff2 = analysis.GetOrAddType(new FuncTypeSlotData(fff1), log);
						analysis.UpdateType(slot, fff2, log);
						break;
					}
					if (ident == "int")
					{
						analysis.ReplaceData(slot, SlotEnum.Identifier, new IdentifierSlotData("int"), log);
						var dotnetTypeSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(int)), log);
						var metaTypeSlot = analysis.GetOrAddType(new MetaTypeSlotData(dotnetTypeSlot), log);
						analysis.UpdateType(slot, metaTypeSlot, log);
						break;
					}
					if (ident == "bool")
					{
						analysis.ReplaceData(slot, SlotEnum.Intrinsic, new IdentifierSlotData("bool"), log);
						var dotnetTypeSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(bool)), log);
						var metaTypeSlot = analysis.GetOrAddType(new MetaTypeSlotData(dotnetTypeSlot), log);
						analysis.UpdateType(slot, metaTypeSlot, log);
						break;
					}
					if (ident == "if")
					{
						var callNode = analysis.GetSlot(node.Parent);
						var callData = analysis.GetData<CallSlotData>(node.Parent);
						if (callData.Args.Length != 2)
						{
							throw new InvalidOperationException("Invalid if statement");
						}
						var ifData = new IfSlotData(Condition: callData.Args[0], Body: callData.Args[1]);
						// var ifSlot = table.Add(node.ParentSlot, TableSlotType.If, ifData, log);
						analysis.ReplaceData(node.Parent, SlotEnum.If, ifData, log);
						analysis.ReplaceData(slot, SlotEnum.Unknown, InvalidSlotData.Instance, log); // invalidate id slot
						break;
					}

					log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}0000 = ROOT");
					break;
				}
				currentNode = analysis.GetSlot(currentSlot);

				if (currentNode.CodeType == SlotEnum.Braces)
				{
					var bracesData = (BracesSlotData)currentNode.Data;
					if (bracesData.TryGetNameTableValue(ident, out var valueSlot))
					{
						log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}{currentSlot:0000} FOUND: {bracesData} = {valueSlot}");
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
				case SlotEnum.Type:
				{
					// types are always added with a meta type
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case SlotEnum.Void:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case SlotEnum.Integer:
				{
					var intSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(int)), log);
					analysis.UpdateType(slot, intSlot, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case SlotEnum.String:
				{
					var stringSlot = analysis.GetOrAddType(new DotnetTypeSlotData(typeof(string)), log);
					analysis.UpdateType(slot, stringSlot, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case SlotEnum.Intrinsic:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					log.WriteLine($"intrinsic: {slot:0000} = {node}");
					// TODO: intrinsic types
					break;
				}
				case SlotEnum.Parameter:
				{
					// var intType = analysis.GetOrAddType(ParameterTypeData.Instance, log);
					analysis.UpdateType(slot, 0, log);
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
			var sourceSlot = analysis.GetSlot(sourceSlotId);
			var sourceTypeId2 = sourceSlot.TypeSlot;
			log.WriteLine($"dequeue: {sourceSlotId:0000}");

			// find all nodes that reference this node
			// future: build a reverse lookup table on an earlier pass
			// note two targets to be refreshed with each update: 1) parent, 2) arbitrary references
			foreach (var (targetSlotId, targetSlot) in analysis.All.Index())
			{
				if (targetSlot.TypeSlot != 0) { continue; } // already set
				if (targetSlot.CodeType == SlotEnum.Declare)
				{
					var declareData = analysis.GetData<DeclareSlotData>(targetSlotId);

					if (declareData.Type == sourceSlotId)
					{
						var typeSlot = analysis.GetSlot(declareData.Type);
						if (typeSlot.CodeType == SlotEnum.Identifier) // follow identifier to actual type
						{
							var idData = analysis.GetData<IdentifierSlotData>(declareData.Type);
							analysis.UpdateType(targetSlotId, idData.Target, log);
							sourceQueue.Enqueue(targetSlotId);
							continue;
						}
					}
					else if (declareData.Value == sourceSlotId && declareData.Type == 0)
					{
						var valueSlotId = declareData.Value;
						var valueSlot = analysis.GetSlot(valueSlotId);
						if (valueSlot.TypeSlot is { } typeSlot2 and > 0)
						{
							var sss1 = analysis.GetSlot(typeSlot2);
							var sss2 = analysis.GetData<TypeSlotData>(typeSlot2);
							analysis.UpdateType(targetSlotId, typeSlot2, log);
							sourceQueue.Enqueue(targetSlotId);
							log.WriteLine($"slot {targetSlotId:0000} declare: NEW TYPE <- {typeSlot2}");
							// throw new InvalidOperationException($"TYPE2: {sss2}");
						}
					}
				}
				else if (targetSlot.CodeType == SlotEnum.Add)
				{
					var addSlot = analysis.GetSlot(targetSlotId);
					var addData = analysis.GetData<AddSlotData>(targetSlotId);

					// skip if neither operand matches source slot
					if (addData.Left != sourceSlotId && addData.Right != sourceSlotId) { continue; }

					var leftType = analysis.GetSlot(addData.Left).TypeSlot;
					var rightType = analysis.GetSlot(addData.Right).TypeSlot;

					// skip if both operands are not known yet
					if (leftType == 0 || rightType == 0) { continue; }

					// type coercion not supported yet
					if (leftType != rightType)
					{
						log.WriteLine($"slot {targetSlotId:0000} add: {leftType} != {rightType}");
						throw new InvalidOperationException($"Type of add operands do not match: {leftType} != {rightType}");
					}

					analysis.UpdateType(targetSlotId, leftType, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.Identifier)
				{
					var idSlot = analysis.GetSlot(targetSlotId);
					var idData = analysis.GetData<IdentifierSlotData>(targetSlotId);

					if (idData.Target != sourceSlotId) { continue; }

					if (sourceSlot.CodeType == SlotEnum.Type)
					{
						// identifier to a type must turn into a type
						var metaSlot = analysis.GetOrAddType(new MetaTypeSlotData(sourceSlotId), log);
						analysis.UpdateType(targetSlotId, metaSlot, log);
						sourceQueue.Enqueue(targetSlotId);
						continue;
					}

					if (sourceSlot.CodeType == SlotEnum.Declare)
					{
						// identifier to a declaration returns the type of that declaration

						// identifier to a function must turn into a call
						var ttt = analysis.GetSlot(sourceTypeId2);
						if (ttt.Data is FuncTypeSlotData fff)
						{
							var parentSlot = analysis.GetSlot(targetSlot.Parent);
							if (parentSlot.CodeType != SlotEnum.Call)
							{
								var newIdSlotId = analysis.Add(targetSlotId, SlotEnum.Identifier, idData, log);
								analysis.UpdateType(newIdSlotId, sourceTypeId2, log);
								analysis.ReplaceData(targetSlotId, SlotEnum.Call, new CallSlotData(Target: newIdSlotId, Args: []), log);
								sourceQueue.Enqueue(newIdSlotId);
								sourceQueue.Enqueue(targetSlotId);
								continue;
							}
						}

						analysis.UpdateType(targetSlotId, sourceTypeId2, log);
						sourceQueue.Enqueue(targetSlotId);
						continue;
					}

					throw new InvalidOperationException($"Type check failed for identifier");
				}
				else if (targetSlot.CodeType == SlotEnum.Return)
				{
					var returnSlot = analysis.GetSlot(targetSlotId);
					var returnData = analysis.GetData<ReturnSlotData>(targetSlotId);

					if (returnData.Value != sourceSlotId) { continue; }

					log.WriteLine($"slot {targetSlotId:0000} return: type <- {sourceTypeId2:0000} via {returnData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.Braces)
				{
					var bracesSlot = analysis.GetSlot(targetSlotId);
					var bracesData = analysis.GetData<BracesSlotData>(targetSlotId);

					if (sourceSlot.CodeType != SlotEnum.Return) { continue; } // should be redundant
					var returnData = (ReturnSlotData)sourceSlot.Data;
					if (returnData.Function != targetSlotId) { continue; } // should be redundant

					var bracesType = analysis.GetOrAddType(new FuncTypeSlotData(sourceTypeId2), log);
					log.WriteLine($"slot {targetSlotId:0000} braces: found return {sourceSlotId:0000} ({bracesType:0000})");
					analysis.UpdateType(targetSlotId, bracesType, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.Call)
				{
					var callSlot = analysis.GetSlot(targetSlotId);
					var callData = analysis.GetData<CallSlotData>(targetSlotId);

					// match dequeued source slot
					if (callData.Target != sourceSlotId && !callData.Args.Contains(sourceSlotId)) { continue; }
					var callTargetSlot = analysis.GetSlot(callData.Target);

					// skip if target or arg types are not known yet
					if (callTargetSlot.TypeSlot <= 0) { continue; }
					if (callData.Args.Any(i => analysis.GetSlot(i).TypeSlot == 0)) { continue; }

					var callTargetType2 = analysis.GetSlot(callTargetSlot.TypeSlot);
					var callTargetTypeData2 = analysis.GetData<TypeSlotData>(callTargetSlot.TypeSlot);
					if (callTargetTypeData2 is FuncTypeSlotData { ReturnType: var returnType })
					{
						analysis.UpdateType(targetSlotId, returnType, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else if (callTargetTypeData2 is DotnetMemberTypeSlotData { TargetType: { } ttt, MemberType: { } dotnetMemberType, Members: { } dotnetMembers })
					{
						var callMemberTargetData = analysis.GetData<MemberSlotData>(callData.Target);

						if (dotnetMemberType != MemberTypes.Method) { throw new InvalidOperationException("Invalid dotnet member call type"); }

						var dotnetArgs = new Type[callData.Args.Length];
						foreach (var (i, arg) in callData.Args.Index())
						{
							var argSlot = analysis.GetSlot(arg);
							var argType = analysis.GetSlot(argSlot.TypeSlot);
							if (argType.CodeType != SlotEnum.Type || argType.Data is not DotnetTypeSlotData { Type: { } dotnetType }) { throw new InvalidOperationException($"Invalid arg type: {argType} {argSlot}"); }
							dotnetArgs[i] = dotnetType;
						}

						MethodInfo? found = null;
						foreach (var memberInfo in dotnetMembers)
						{
							if (memberInfo is not MethodInfo methodInfo) { continue; }
							if (methodInfo.GetParameters().Length != callData.Args.Length) { continue; }
							if (!methodInfo.GetParameters().Select(i => i.ParameterType).SequenceEqual(dotnetArgs)) { continue; }
							found = methodInfo;
							break;
						}

						if (found is null) { throw new InvalidOperationException("Invalid member call"); }

						var returnDotNetTypeId = analysis.GetOrAddType(new DotnetTypeSlotData(found.ReturnType), log);

						analysis.UpdateData(targetSlotId, callData with { DotnetMethod = found }, log);
						analysis.UpdateType(targetSlotId, returnDotNetTypeId, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else
					{
						throw new InvalidOperationException($"Invalid call target type:\n{callTargetTypeData2}\n{callTargetSlot.TypeSlot}");
					}
				}
				else if (targetSlot.CodeType == SlotEnum.Assign)
				{
					var assignSlot = analysis.GetSlot(targetSlotId);
					var assignData = analysis.GetData<AssignSlotData>(targetSlotId);

					if (assignData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetSlot(sourceTypeId2);
					log.WriteLine($"slot {targetSlotId:0000} assign: type <- {typeRow} {sourceTypeId2:0000} via {assignData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.Member)
				{
					var memberSlot = analysis.GetSlot(targetSlotId);
					var memberData = analysis.GetData<MemberSlotData>(targetSlotId);

					if (memberData.Target != sourceSlotId && memberData.Member != sourceSlotId) { continue; }

					var leftSlot = analysis.GetSlot(memberData.Target);
					var rightSlot = analysis.GetSlot(memberData.Member);

					if (leftSlot.TypeSlot == 0) { continue; }

					if (rightSlot.CodeType != SlotEnum.Identifier) { throw new InvalidOperationException($"Invalid member type: {rightSlot.CodeType}"); }
					var rightData = analysis.GetData<IdentifierSlotData>(memberData.Member);
					var rightName = rightData.Name;

					var leftTypeSlot = analysis.GetSlot(leftSlot.TypeSlot);
					if (leftTypeSlot.Data is DotnetTypeSlotData { Type: { } dotnetType2 })
					{
						var members = dotnetType2.GetMember(rightName);
						if (members.Length == 0)
						{
							throw new InvalidOperationException($"Invalid member: {rightName}");
						}

						var memberType = members[0].MemberType;
						if (!members.All(i => i.MemberType == memberType))
						{
							throw new InvalidOperationException("Mixed member types");
						}

						var memberTypeData = new DotnetMemberTypeSlotData(
							TargetType: leftSlot.TypeSlot,
							MemberName: rightName,
							MemberType: memberType,
							Members: members);
						var memberTypeId = analysis.GetOrAddType(memberTypeData, log);
						analysis.UpdateType(targetSlotId, memberTypeId, log);
						sourceQueue.Enqueue(targetSlotId);

						analysis.UpdateType(memberData.Member, memberTypeId, log);
						sourceQueue.Enqueue(memberData.Member);

						continue;
					}
					else
					{
						throw new NotImplementedException($"Invalid type: {leftTypeSlot}");
					}
				}
				else if (targetSlot.CodeType == SlotEnum.LogicalNegation)
				{
					var negationSlot = analysis.GetSlot(targetSlotId);
					var negationData = analysis.GetData<LogicalNegationSlotData>(targetSlotId);

					if (negationData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetSlot(sourceTypeId2);
					log.WriteLine($"slot {targetSlotId:0000} not: type <- {typeRow} {sourceTypeId2:0000} via {negationData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.Negation)
				{
					var negationSlot = analysis.GetSlot(targetSlotId);
					var negationData = analysis.GetData<NegationSlotData>(targetSlotId);

					if (negationData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetSlot(sourceTypeId2);
					log.WriteLine($"slot {targetSlotId:0000} negation: type <- {typeRow} {sourceTypeId2:0000} via {negationData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == SlotEnum.If)
				{
					var ifSlot = analysis.GetSlot(targetSlotId);
					var ifData = analysis.GetData<IfSlotData>(targetSlotId);

					if (ifData.Body != sourceSlotId && ifData.Condition != sourceSlotId) { continue; } // not relevant

					var condSlot = analysis.GetSlot(ifData.Condition);
					if (condSlot.TypeSlot == 0) { continue; } // not ready

					var bodySlot = analysis.GetSlot(ifData.Body);
					if (bodySlot.TypeSlot == 0) { continue; } // not ready
					var bodyTypeData = analysis.GetSlot(bodySlot.TypeSlot);

					if (bodySlot.CodeType != SlotEnum.Braces) { throw new InvalidOperationException($"Invalid if body: {bodySlot.CodeType}"); }

					if (bodyTypeData.Data is not FuncTypeSlotData)
					{
						throw new InvalidOperationException("Invalid if body type");
					}

					log.WriteLine($"slot {targetSlotId:0000} if: type <- {sourceTypeId2:0000} via {ifData.Condition:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId2, log);
					sourceQueue.Enqueue(targetSlotId);
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
		var data = new DeclareSlotData(Name: id, IsStatic: isStatic);
		var slot = analysis.Add(parentSlot, SlotEnum.Declare, data, log);

		// Name
		if (!analysis.TryGetSlot<BracesSlotData>(parentSlot, SlotEnum.Braces, out var bracesData, log))
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