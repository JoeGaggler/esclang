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

	// TODO: these should looked up as identifiers in the current scope when needed
	[Obsolete]
	public const int TODO_SLOT = -13;
	[Obsolete]
	public const int FAKE_GLOBAL_PARENT = 2;
	private static int TYPE_SLOT;
	private static int FUNC_SLOT;
	private static int DYNAMIC_SLOT;
	[Obsolete]
	private static int DOTNET_SLOT;
	private static int VOID_SLOT;
	private static int INT_SLOT;
	private static int BOOL_SLOT;
	private static int STRING_SLOT;

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

				// type is a type!
				var typeSlot = TYPE_SLOT = analysis.Add(bracesSlot, CodeSlotEnum.Type, new TypeCodeData(Name: "TYPE"), log);
				analysis.UpdateType(typeSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("type", typeSlot);

				// dynamic
				var dynamicTypeSlot = DYNAMIC_SLOT = analysis.GetOrAddType2(new TypeCodeData(Name: "DYNAMIC"), log);
				analysis.UpdateType(dynamicTypeSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("dynamic", dynamicTypeSlot);

				// func
				var funcTypeSlot = FUNC_SLOT = analysis.GetOrAddType2(new TypeCodeData(Name: "FUNC"), log);
				analysis.UpdateType(funcTypeSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("func", funcTypeSlot);

				// dotnet
				var dotnetTypeSlot = DOTNET_SLOT = analysis.GetOrAddType2(new TypeCodeData(Name: "DOTNET"), log);
				analysis.UpdateType(dotnetTypeSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("dotnet", dynamicTypeSlot);

				// void
				var voidSlot = VOID_SLOT = analysis.GetOrAddType2(new DotnetTypeCodeData(Name: "VOID", typeof(void)), log);
				analysis.UpdateType(voidSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("void", voidSlot);

				// int -> identifier for an instance of type that represents a dotnet integer
				var intSlot = INT_SLOT = analysis.GetOrAddType2(new DotnetTypeCodeData(Name: "INT", typeof(int)), log);
				analysis.UpdateType(intSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("int", intSlot);

				// bool -> identifier for an instance of type that represents a dotnet boolean
				var boolSlot = BOOL_SLOT = analysis.GetOrAddType2(new DotnetTypeCodeData(Name: "BOOL", typeof(bool)), log);
				analysis.UpdateType(boolSlot, 0, typeSlot, log);
				bracesData.TryAddNameTableValue("bool", boolSlot);

				// string
				var stringSlot = STRING_SLOT = analysis.GetOrAddType2(new DotnetTypeCodeData(Name: "STRING", typeof(string)), log);
				analysis.UpdateType(stringSlot, 0, typeSlot, log);
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
			case NegationNode { Node: { } innerNode }:
			{
				var innerSlotId = BuildTable(innerNode, parentSlot, analysis, log);
				var data = new NegationCodeData(Value: innerSlotId);
				var slot = analysis.Add(parentSlot, CodeSlotEnum.Negation, data, log);

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
				var data = new MemberCodeData(0, 0);
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
				var retVoid = analysis.GetOrAddType(new FunctionTypeData(voidType, VOID_SLOT), log);
				analysis.UpdateType(slot, retVoid, VOID_SLOT, log);
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
					analysis.UpdateType(voidSlot, voidType, VOID_SLOT, log);

					var returnData = new ReturnCodeData(voidSlot);
					analysis.ReplaceData(slot, CodeSlotEnum.Return, returnData, log);
					analysis.UpdateType(slot, voidType, VOID_SLOT, log);
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

				log.WriteLine($"slot {slot:0000} in {node.Parent:0000} -- call -> return");
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
			var currentSlot = node.Parent;
			while (true)
			{
				// not embedded in a braces node?
				if (currentSlot == 0)
				{
					throw new InvalidOperationException("Return statement not in a braces node");
				}

				var currentNode = analysis.GetCodeSlot(currentSlot);
				if (currentNode.CodeType == CodeSlotEnum.Braces)
				{
					var bracesData = (BracesCodeData)currentNode.Data;

					log.WriteLine($"slot {slot:0000} <- returns to {currentSlot:0000}");

					analysis.UpdateData(slot, returnData with { Function = currentSlot }, log);

					// clear "proc" type since we found a return statement
					analysis.UpdateType(currentSlot, 0, 0, log);
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

				var currentSlot = currentNode.Parent;
				if (currentSlot == 0)
				{
					// check for intrinsics
					if (ident == "true")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Boolean, new BooleanCodeData(true), log);
						var fun = analysis.GetOrAddType(new DotnetTypeData(typeof(bool)), log);
						analysis.UpdateType(slot, fun, BOOL_SLOT, log);
						break;
					}
					if (ident == "false")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Boolean, new BooleanCodeData(false), log);
						var fun = analysis.GetOrAddType(new DotnetTypeData(typeof(bool)), log);
						analysis.UpdateType(slot, fun, BOOL_SLOT, log);
						break;
					}
					if (ident == "print")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Intrinsic, new IntrinsicCodeData("print"), log);
						var fff1 = analysis.GetOrAddType2(new TypeCodeData("STRING"), log);
						var fff2 = analysis.GetOrAddType2(new FuncTypeCodeData("print func", fff1), log);
						analysis.UpdateType(slot, 0, fff2, log);
						break;
					}
					if (ident == "int")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Identifier, new IdentifierCodeData("int"), log);
						var type = analysis.GetOrAddType(new DotnetTypeData(typeof(int)), log);
						var meta = analysis.GetOrAddType(TypeTypeData.Instance, log);
						analysis.UpdateType(slot, meta, TYPE_SLOT, log);
						break;
					}
					if (ident == "bool")
					{
						analysis.ReplaceData(slot, CodeSlotEnum.Intrinsic, new IdentifierCodeData("bool"), log);
						var type = analysis.GetOrAddType(new DotnetTypeData(typeof(bool)), log);
						var meta = analysis.GetOrAddType(TypeTypeData.Instance, log);
						analysis.UpdateType(slot, meta, TYPE_SLOT, log);
						break;
					}
					if (ident == "if")
					{
						var callNode = analysis.GetCodeSlot(node.Parent);
						var callData = analysis.GetCodeData<CallCodeData>(node.Parent);
						if (callData.Args.Length != 2)
						{
							throw new InvalidOperationException("Invalid if statement");
						}
						var ifData = new IfSlotCodeData(Condition: callData.Args[0], Body: callData.Args[1]);
						// var ifSlot = table.Add(node.ParentSlot, TableSlotType.If, ifData, log);
						analysis.ReplaceData(node.Parent, CodeSlotEnum.If, ifData, log);
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
				case CodeSlotEnum.Type:
				{
					// var typeData = (TypeCodeData)node.Data;
					// var typeSlot = analysis.GetCodeSlot(slot);
					// var type = typeData.Type;
					// var typeType = analysis.GetOrAddType(TypeTypeData.Instance, log);
					analysis.UpdateType(slot, 0, TYPE_SLOT, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.Void:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.Integer:
				{
					var intType = analysis.GetOrAddType(new DotnetTypeData(typeof(int)), log);
					analysis.UpdateType(slot, intType, INT_SLOT, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case CodeSlotEnum.String:
				{
					var strType = analysis.GetOrAddType(new DotnetTypeData(typeof(string)), log);
					analysis.UpdateType(slot, strType, STRING_SLOT, log);
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
					// var intType = analysis.GetOrAddType(ParameterTypeData.Instance, log);
					analysis.UpdateType(slot, 0, DYNAMIC_SLOT, log);
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
			var sourceSlot = analysis.GetCodeSlot(sourceSlotId);
			var sourceTypeId = sourceSlot.TypeSlot;
			var sourceTypeData = analysis.GetTypeData(sourceTypeId);
			log.WriteLine($"dequeue: {sourceSlotId:0000}");

			// find all nodes that reference this node
			// future: build a reverse lookup table on an earlier pass
			// note two targets to be refreshed with each update: 1) parent, 2) arbitrary references
			foreach (var (targetSlotId, targetSlot) in analysis.All.Index())
			{
				if (targetSlot.TypeSlot2 != 0) { continue; } // already set
				if (targetSlot.CodeType == CodeSlotEnum.Declare)
				{
					var declareData = analysis.GetCodeData<DeclareCodeData>(targetSlotId);

					if (declareData.Type == sourceSlotId)
					{
						var typeSlot = analysis.GetCodeSlot(declareData.Type);
						if (typeSlot.CodeType == CodeSlotEnum.Identifier) // follow identifier to actual type
						{
							var idData = analysis.GetCodeData<IdentifierCodeData>(declareData.Type);
							analysis.UpdateType(targetSlotId, 0/*idTypeSlot*/, idData.Target, log);
							sourceQueue.Enqueue(targetSlotId);
							continue;
						}

						throw new InvalidOperationException($"BLAHHHH: {typeSlot}");

						var typeData = analysis.GetTypeData(typeSlot.TypeSlot);
						if (typeData is TypeTypeData)
						{
							var idData = analysis.GetCodeData<IdentifierCodeData>(declareData.Type);
							var idTypeValue = analysis.GetCodeSlot(idData.Target); // slot where the instance of the type is defined
																				   // var idTypeData = analysis.GetCodeData<TypeCodeData>(idData.Target);
																				   // var idTypeSlot = idTypeData.Type; // known type this declaration value
							analysis.UpdateType(targetSlotId, 0/*idTypeSlot*/, idData.Target, log);
							sourceQueue.Enqueue(targetSlotId);
						}
						else
						{
							throw new NotImplementedException($"Invalid type: {typeData}");
						}
					}
					else if (declareData.Value == sourceSlotId && declareData.Type == 0)
					{
						var valueSlotId = declareData.Value;
						var valueSlot = analysis.GetCodeSlot(valueSlotId);
						if (valueSlot.TypeSlot2 is { } typeSlot2 and > 0)
						{
							var sss1 = analysis.GetCodeSlot(typeSlot2);
							var sss2 = analysis.GetCodeData<TypeCodeData>(typeSlot2);
							analysis.UpdateType(targetSlotId, 0, typeSlot2, log);
							sourceQueue.Enqueue(targetSlotId);
							log.WriteLine($"slot {targetSlotId:0000} declare: NEW TYPE <- {typeSlot2}");
							// throw new InvalidOperationException($"TYPE2: {sss2}");
						}
						else
						{
							var valueSlotType = valueSlot.TypeSlot;
							analysis.UpdateType(targetSlotId, valueSlotType, TODO_SLOT, log);
							sourceQueue.Enqueue(targetSlotId);
							log.WriteLine($"slot {targetSlotId:0000} declare: LEGACY TYPE <- {sourceTypeId} {sourceSlotId}");
						}
					}
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Add)
				{
					var addSlot = analysis.GetCodeSlot(targetSlotId);
					var addData = analysis.GetCodeData<AddOpCodeData>(targetSlotId);

					// skip if neither operand matches source slot
					if (addData.Left != sourceSlotId && addData.Right != sourceSlotId) { continue; }

					// var leftType = analysis.GetCodeSlot(addData.Left).TypeSlot;
					// var rightType = analysis.GetCodeSlot(addData.Right).TypeSlot;
					var leftType = analysis.GetCodeSlot(addData.Left).TypeSlot2;
					var rightType = analysis.GetCodeSlot(addData.Right).TypeSlot2;

					// skip if both operands are not known yet
					if (leftType == 0 || rightType == 0) { continue; }

					// type coercion not supported yet
					if (leftType != rightType)
					{
						log.WriteLine($"slot {targetSlotId:0000} add: {leftType} != {rightType}");
						// throw new InvalidOperationException($"Type of add operands do not match: {leftType} != {rightType}");
						continue;
					}

					// analysis.UpdateType(targetSlotId, leftType, TODO_SLOT, log);
					analysis.UpdateType(targetSlotId, 0, leftType, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Identifier)
				{
					var idSlot = analysis.GetCodeSlot(targetSlotId);
					var idData = analysis.GetCodeData<IdentifierCodeData>(targetSlotId);

					if (idData.Target != sourceSlotId) { continue; }

					// var typeCodeData = analysis.GetCodeData<TypeCodeData>(sourceSlotId);
					// throw new InvalidOperationException($"Invaasdfasdfsadflid type: {typeCodeData}");

					if (sourceSlot.CodeType == CodeSlotEnum.Type)
					{
						// identifier to a type must turn into a type
						analysis.UpdateType(targetSlotId, 0, TYPE_SLOT, log);
						sourceQueue.Enqueue(targetSlotId);
						continue;
					}

					if (sourceSlot.CodeType == CodeSlotEnum.Declare)
					{
						// identifier to a declaration returns the type of that declaration

						// identifier to a function must turn into a call
						var ttt = analysis.GetCodeSlot(sourceSlot.TypeSlot2);
						if (ttt.Data is FuncTypeCodeData fff)
						{
							var parentSlot = analysis.GetCodeSlot(targetSlot.Parent);
							if (parentSlot.CodeType != CodeSlotEnum.Call)
							{
								var newIdSlotId = analysis.Add(targetSlotId, CodeSlotEnum.Identifier, idData, log);
								analysis.UpdateType(newIdSlotId, 0, sourceSlot.TypeSlot2, log);
								analysis.ReplaceData(targetSlotId, CodeSlotEnum.Call, new CallCodeData(Target: newIdSlotId, Args: []), log);
								sourceQueue.Enqueue(newIdSlotId);
								sourceQueue.Enqueue(targetSlotId);
								continue;
							}
						}

						analysis.UpdateType(targetSlotId, 0, sourceSlot.TypeSlot2, log);
						sourceQueue.Enqueue(targetSlotId);
						continue;
					}

					throw new InvalidOperationException($"Type check failed for identifier");
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Return)
				{
					var returnSlot = analysis.GetCodeSlot(targetSlotId);
					var returnData = analysis.GetCodeData<ReturnCodeData>(targetSlotId);

					if (returnData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetTypeData(sourceTypeId);
					log.WriteLine($"slot {targetSlotId:0000} return: type <- {typeRow} {sourceTypeId} via {returnData.Value:0000} {sourceSlot.TypeSlot2:0000}x");
					analysis.UpdateType(targetSlotId, sourceTypeId, sourceSlot.TypeSlot2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Braces)
				{
					var bracesSlot = analysis.GetCodeSlot(targetSlotId);
					var bracesData = analysis.GetCodeData<BracesCodeData>(targetSlotId);

					if (sourceSlot.CodeType != CodeSlotEnum.Return) { continue; } // should be redundant
					var returnData = (ReturnCodeData)sourceSlot.Data;
					if (returnData.Function != targetSlotId) { continue; } // should be redundant

					var returnType = analysis.GetTypeData(sourceTypeId);
					var funcType = new FunctionTypeData(sourceTypeId, sourceSlot.TypeSlot2);
					var funcTypeId = analysis.GetOrAddType(funcType, log);

					// TODO: FIND EXISTING FUNCTION TYPE

					int? funcType2 = null;
					CodeSlot? funcType2Slot = null;
					foreach (var (i, j) in analysis.All.Index())
					{
						if (j.CodeType != CodeSlotEnum.Type) { continue; }
						if (j.Data is not TypeCodeData { Name: { } nnn }) { continue; }
						if (nnn != $"FUNC -> {sourceSlot.TypeSlot2}") { continue; }
						funcType2 = i;
						funcType2Slot = j;
						break;
					}
					if (funcType2 is not null && funcType2Slot is not null)
					{
						log.WriteLine($"slot {targetSlotId:0000} braces: found return {sourceSlotId:0000} ({funcTypeId}, {funcType2.Value:0000})");
						analysis.UpdateType(targetSlotId, funcTypeId, funcType2.Value, log);
						sourceQueue.Enqueue(targetSlotId);
					}
					else
					{
						// var sss = analysis.Add(2, CodeSlotEnum.Type, new TypeCodeData(Name: $"FUNC -> {sourceSlot.TypeSlot2}"), log);
						var sss = analysis.Add(FAKE_GLOBAL_PARENT, CodeSlotEnum.Type, new FuncTypeCodeData(Name: $"FUNC -> {sourceSlot.TypeSlot2}", sourceSlot.TypeSlot2), log);
						analysis.UpdateType(sss, 0, TYPE_SLOT, log);

						log.WriteLine($"slot {targetSlotId:0000} braces: found return {sourceSlotId:0000} ({funcTypeId}, {sss:0000})");
						analysis.UpdateType(targetSlotId, funcTypeId, sss, log);
						sourceQueue.Enqueue(targetSlotId);
					}
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Call)
				{
					var callSlot = analysis.GetCodeSlot(targetSlotId);
					var callData = analysis.GetCodeData<CallCodeData>(targetSlotId);

					// match dequeued source slot
					if (callData.Target != sourceSlotId && !callData.Args.Contains(sourceSlotId)) { continue; }
					var callTargetSlot = analysis.GetCodeSlot(callData.Target);
					// log.WriteLine($"DOTNET_SLOT4: {targetSlotId} -< {sourceSlotId} {sourceSlot}");

					// skip if target or arg types are not known yet
					if (callTargetSlot.TypeSlot2 <= 0) { continue; }
					if (callData.Args.Any(i => analysis.GetCodeSlot(i).TypeSlot2 == 0)) { continue; }

					var callTargetType2 = analysis.GetCodeSlot(callTargetSlot.TypeSlot2);
					var callTargetTypeData2 = analysis.GetCodeData<TypeCodeData>(callTargetSlot.TypeSlot2);
					// callTargetTypeData2.Name

					log.WriteLine($"DOTNET_SLOT3: {targetSlotId} -< {callTargetType2}");

					var callTargetType = analysis.GetTypeData(callTargetSlot.TypeSlot);
					if (callTargetTypeData2 is FuncTypeCodeData { ReturnType: var returnType })
					{
						log.WriteLine($"DOTNET_SLOT2: {targetSlotId} -< {sourceSlot} -< {returnType}");
						analysis.UpdateType(targetSlotId, 0, returnType, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else if (callTargetTypeData2 is DotnetMemberTypeCodeData { TargetType: { } ttt, MemberType: { } dotnetMemberType, Members: { } dotnetMembers })
					{
						var callMemberTargetData = analysis.GetCodeData<MemberCodeData>(callData.Target);

						if (dotnetMemberType != MemberTypes.Method) { throw new InvalidOperationException("Invalid dotnet member call type"); }

						var dotnetArgs = new Type[callData.Args.Length];
						foreach (var (i, arg) in callData.Args.Index())
						{
							var argSlot = analysis.GetCodeSlot(arg);
							var argType = analysis.GetCodeSlot(argSlot.TypeSlot2);
							if (argType.CodeType != CodeSlotEnum.Type || argType.Data is not DotnetTypeCodeData { Type: { } dotnetType }) { throw new InvalidOperationException($"Invalid arg type: {argType} {argSlot}"); }
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

						// var returnDotNetType = new DotnetTypeData(found.ReturnType);
						// var returnDotNetTypeId = analysis.GetOrAddType(returnDotNetType, log);

						log.WriteLine("DOTNET_SLOT1");


						analysis.UpdateData(targetSlotId, callData with { DotnetMethod = found }, log);
						analysis.UpdateType(targetSlotId, 0/*returnDotNetTypeId*/, DOTNET_SLOT, log);
						sourceQueue.Enqueue(targetSlotId);
						break;
					}
					else
					{
						throw new InvalidOperationException($"Invalid call target type:\n{callTargetType}\n{callTargetTypeData2}\n{callTargetSlot.TypeSlot2}");
					}
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Assign)
				{
					var assignSlot = analysis.GetCodeSlot(targetSlotId);
					var assignData = analysis.GetCodeData<AssignCodeData>(targetSlotId);

					if (assignData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetCodeSlot(sourceSlot.TypeSlot2);
					log.WriteLine($"slot {targetSlotId:0000} assign: type <- {typeRow} {sourceTypeId} via {assignData.Value:0000}");
					analysis.UpdateType(targetSlotId, 0, sourceSlot.TypeSlot2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Member)
				{
					var memberSlot = analysis.GetCodeSlot(targetSlotId);
					var memberData = analysis.GetCodeData<MemberCodeData>(targetSlotId);

					if (memberData.Target != sourceSlotId && memberData.Member != sourceSlotId) { continue; }

					var leftSlot = analysis.GetCodeSlot(memberData.Target);
					var rightSlot = analysis.GetCodeSlot(memberData.Member);

					if (leftSlot.TypeSlot2 == 0) { continue; }

					if (rightSlot.CodeType != CodeSlotEnum.Identifier) { throw new InvalidOperationException($"Invalid member type: {rightSlot.CodeType}"); }
					var rightData = analysis.GetCodeData<IdentifierCodeData>(memberData.Member);
					var rightName = rightData.Name;

					var leftTypeSlot = analysis.GetCodeSlot(leftSlot.TypeSlot2);
					if (leftTypeSlot.Data is DotnetTypeCodeData { Name: { }, Type: { } dotnetType2 })
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

						var memberTypeData = new DotnetMemberTypeCodeData(
							Name: "TODO_NAME_MEMBER_THING",
							TargetType: leftSlot.TypeSlot,
							MemberName: rightName,
							MemberType: memberType,
							Members: members);
						var memberTypeId = analysis.GetOrAddType2(memberTypeData, log);
						analysis.UpdateType(targetSlotId, 0, memberTypeId, log);
						sourceQueue.Enqueue(targetSlotId);

						analysis.UpdateType(memberData.Member, 0, memberTypeId, log);
						sourceQueue.Enqueue(memberData.Member);

						continue;
					}
					else
					{
						throw new NotImplementedException($"Invalid type: {leftTypeSlot}");
					}
				}
				else if (targetSlot.CodeType == CodeSlotEnum.LogicalNegation)
				{
					var negationSlot = analysis.GetCodeSlot(targetSlotId);
					var negationData = analysis.GetCodeData<LogicalNegationCodeData>(targetSlotId);

					if (negationData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetTypeData(sourceTypeId);
					log.WriteLine($"slot {targetSlotId:0000} not: type <- {typeRow} {sourceTypeId} via {negationData.Value:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId, TODO_SLOT, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.Negation)
				{
					var negationSlot = analysis.GetCodeSlot(targetSlotId);
					var negationData = analysis.GetCodeData<NegationCodeData>(targetSlotId);

					if (negationData.Value != sourceSlotId) { continue; }

					var typeRow = analysis.GetCodeSlot(sourceSlot.TypeSlot2);
					log.WriteLine($"slot {targetSlotId:0000} negation: type <- {typeRow} {sourceTypeId} via {negationData.Value:0000}");
					analysis.UpdateType(targetSlotId, 0, sourceSlot.TypeSlot2, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.CodeType == CodeSlotEnum.If)
				{
					var ifSlot = analysis.GetCodeSlot(targetSlotId);
					var ifData = analysis.GetCodeData<IfSlotCodeData>(targetSlotId);

					if (ifData.Body != sourceSlotId && ifData.Condition != sourceSlotId) { continue; } // not relevant

					var condSlot = analysis.GetCodeSlot(ifData.Condition);
					if (condSlot.TypeSlot == 0) { continue; } // not ready

					var bodySlot = analysis.GetCodeSlot(ifData.Body);
					if (bodySlot.TypeSlot == 0) { continue; } // not ready
					var bodyTypeData = analysis.GetTypeData(bodySlot.TypeSlot);

					if (bodySlot.CodeType != CodeSlotEnum.Braces) { throw new InvalidOperationException($"Invalid if body: {bodySlot.CodeType}"); }

					if (bodyTypeData is not FunctionTypeData)
					{
						throw new InvalidOperationException("Invalid if body type");
					}

					log.WriteLine($"slot {targetSlotId:0000} if: type <- {sourceTypeData} via {ifData.Condition:0000}");
					analysis.UpdateType(targetSlotId, sourceTypeId, TODO_SLOT, log);
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