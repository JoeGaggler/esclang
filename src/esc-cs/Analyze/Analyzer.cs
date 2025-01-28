namespace EscLang.Analyze;

using EscLang.Parse;

// TODO: constant enforcement
// TODO: collect declarations before analyzing possible references
// TODO: combine call node handling across step/non-step methods

public static class Analyzer
{
	private static Int32 ScopeCounter = -1;

	public static Table Analyze(Parse.EscFile file, TextWriter log)
	{
		var table = new Table();

		log.WriteLine("=== Build: init ===");
		var fileSlotId = BuildTable(file, 0, table, log);

		log.WriteLine("=== Build: return ===");
		BuildReturn(table, log);

		log.WriteLine("=== Build: resolve identifiers ===");
		BuildResolver(table, log);

		log.WriteLine("=== Build: types ===");
		BuildTypes(table, log);

		log.WriteLine("=== Tree ===");
		Printer.PrintTable(table, log);

		return table;
	}

	private static int BuildTable(SyntaxNode node, int parentSlot, Table table, TextWriter log)
	{
		switch (node)
		{
			case EscFile { Lines: { } lines }:
			{
				var fileData = new FileSlotData();
				var fileSlot = table.Add(parentSlot, TableSlotType.File, fileData, log);

				var bracesData = new BracesSlotData(Lines: []);
				var bracesSlot = table.Add(fileSlot, TableSlotType.Braces, bracesData, log);

				fileData = fileData with { Main = bracesSlot };
				table.UpdateData(fileSlot, fileData, log);

				// Lines
				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, bracesSlot, table, log);
					lineSlots.Add(lineSlot);
				}

				bracesData = bracesData with { Lines = [.. lineSlots] };
				table.UpdateData(bracesSlot, bracesData, log);

				return fileSlot;
			}
			case DeclareStaticNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(true, parentSlot, table, log, idNode, typeNode, valueNode);
			}
			case DeclareAssignNode { Identifier: { } idNode, Type: var typeNode, Value: { } valueNode }:
			{
				return BuildDeclareNode(false, parentSlot, table, log, idNode, typeNode, valueNode);
			}
			case CallNode { Target: { } target, Arguments: { } arguments }:
			{
				var data = new CallSlotData(Target: 0, Args: []);
				var slot = table.Add(parentSlot, TableSlotType.Call, data, log);

				// Target
				var targetSlot = BuildTable(target, slot, table, log);
				data = data with { Target = targetSlot };
				table.UpdateData(slot, data, log);

				// Args
				var argSlots = new List<int>();
				foreach (var arg in arguments)
				{
					var argSlot = BuildTable(arg, slot, table, log);
					argSlots.Add(argSlot);
				}

				data = data with { Args = [.. argSlots] };
				table.UpdateData(slot, data, log);

				return slot;
			}
			case IdentifierNode { Text: { Length: > 0 } id }:
			{
				var data = new IdentifierSlotData(Name: id);
				var slot = table.Add(parentSlot, TableSlotType.Identifier, data, log);

				return slot;
			}
			case BracesNode { Lines: { } lines }:
			{
				var data = new BracesSlotData([]);
				var slot = table.Add(parentSlot, TableSlotType.Braces, data, log);

				var lineSlots = new List<int>();
				foreach (var line in lines)
				{
					var lineSlot = BuildTable(line, slot, table, log);
					lineSlots.Add(lineSlot);
				}

				data = data with { Lines = [.. lineSlots] };
				table.UpdateData(slot, data, log);

				return slot;
			}
			case LiteralNumberNode { Text: { Length: > 0 } numberLiteral }:
			{
				var data = new IntegerSlotData(Value: Int32.Parse(numberLiteral));
				var slot = table.Add(parentSlot, TableSlotType.Integer, data, log);

				return slot;
			}
			case LiteralStringNode { Text: { Length: > 0 } stringLiteral }:
			{
				var data = new StringSlotData(Value: stringLiteral);
				var slot = table.Add(parentSlot, TableSlotType.String, data, log);

				return slot;
			}
			case PlusNode { Left: { } left, Right: { } right }:
			{
				var data = new AddOpSlotData();
				var slot = table.Add(parentSlot, TableSlotType.Add, data, log);

				// Operands
				var leftSlot = BuildTable(left, slot, table, log);
				var rightSlot = BuildTable(right, slot, table, log);

				data = data with { Left = leftSlot, Right = rightSlot };
				table.UpdateData(slot, data, log);

				return slot;
			}
			case ParameterNode:
			{
				var data = new ParameterSlotData();
				var slot = table.Add(parentSlot, TableSlotType.Parameter, data, log);

				return slot;
			}
			case LogicalNegationNode { Node: { } innerNode }:
			{
				var innerSlotId = BuildTable(innerNode, parentSlot, table, log);
				var data = new LogicalNegationSlotData(Value: innerSlotId);
				var slot = table.Add(parentSlot, TableSlotType.LogicalNegation, data, log);

				return slot;
			}
			default:
			{
				log.WriteLine($"unknown node for table: {node}");
				throw new NotImplementedException($"TODO: BuildTable: {node}");
			}
		}
	}

	private static void BuildReturn(Table Table, TextWriter log)
	{
		var returnQueue = new Queue<int>();

		foreach (var (slot, node) in Table.All.Index())
		{
			// all functions are procs unless a return statement is found
			if (node.DataType == TableSlotType.Braces)
			{
				var voidType = Table.GetOrAddType(VoidTypeSlot.Instance, log);
				var retVoid = Table.GetOrAddType(new FunctionTypeSlot(voidType), log);
				Table.UpdateType(slot, retVoid, log);
				continue;
			}

			if (node.DataType != TableSlotType.Call) { continue; }

			var call = (CallSlotData)node.Data;
			if (call.Target == 0) { continue; }
			if (call.Args.Length != 1) { continue; }

			if (!Table.TryGetSlot<IdentifierSlotData>(call.Target, TableSlotType.Identifier, out var targetId, log))
			{
				continue;
			}
			if (targetId.Name != "return") { continue; }

			var argSlot = call.Args[0];
			if (argSlot == 0) { continue; }

			var returnData = new ReturnSlotData(argSlot);
			Table.ReplaceData(slot, TableSlotType.Return, returnData, log);
			returnQueue.Enqueue(slot);

			// Invalidate the "return" identifier
			Table.ReplaceData(call.Target, TableSlotType.Unknown, InvalidSlotData.Instance, log);

			log.WriteLine($"slot {slot:0000} in {node.ParentSlot:0000} -- call -> return");
		}

		while (returnQueue.Count > 0)
		{
			var slot = returnQueue.Dequeue();
			var node = Table.GetSlot(slot);

			if (node.DataType != TableSlotType.Return) { continue; } // should be redundant

			var returnData = (ReturnSlotData)node.Data;
			if (returnData.Function != 0) { continue; }

			// recurse up to find the braces node
			var currentSlot = node.ParentSlot;
			while (true)
			{
				if (currentSlot == 0) { break; } // not embedded in a braces node?

				var currentNode = Table.GetSlot(currentSlot);
				if (currentNode.DataType == TableSlotType.Braces)
				{
					var bracesData = (BracesSlotData)currentNode.Data;

					log.WriteLine($"slot {slot:0000} <- returns to {currentSlot:0000}");

					Table.UpdateData(slot, returnData with { Function = currentSlot }, log);

					// clear "proc" type since we found a return statement
					Table.UpdateType(currentSlot, 0, log);
					break;
				}

				currentSlot = currentNode.ParentSlot;
			}
		}
	}

	private static void BuildResolver(Table table, TextWriter log)
	{
		foreach (var (slot, node) in table.All.Index())
		{
			if (node.DataType != TableSlotType.Identifier) { continue; }
			var slotData = (IdentifierSlotData)node.Data;
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
						table.ReplaceData(slot, TableSlotType.Boolean, new BooleanSlotData(true), log);
						var fun = table.GetOrAddType(new NativeTypeSlot("bool"), log);
						table.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "false")
					{
						table.ReplaceData(slot, TableSlotType.Boolean, new BooleanSlotData(false), log);
						var fun = table.GetOrAddType(new NativeTypeSlot("bool"), log);
						table.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "print")
					{
						table.ReplaceData(slot, TableSlotType.Intrinsic, new IntrinsicSlotData("print"), log);
						var str = table.GetOrAddType(new NativeTypeSlot("string"), log);
						var fun = table.GetOrAddType(new FunctionTypeSlot(str), log);
						table.UpdateType(slot, fun, log);
						break;
					}
					if (ident == "int")
					{
						table.ReplaceData(slot, TableSlotType.Intrinsic, new IntrinsicSlotData("int"), log);
						var fun = table.GetOrAddType(new NativeTypeSlot("int"), log);
						var meta = table.GetOrAddType(new MetaTypeSlot(fun), log);
						table.UpdateType(slot, meta, log);
						break;
					}
					if (ident == "bool")
					{
						table.ReplaceData(slot, TableSlotType.Intrinsic, new IntrinsicSlotData("bool"), log);
						var fun = table.GetOrAddType(new NativeTypeSlot("bool"), log);
						var meta = table.GetOrAddType(new MetaTypeSlot(fun), log);
						table.UpdateType(slot, meta, log);
						break;
					}
					if (ident == "if")
					{
						var (callNode, callData) = table.GetSlotTuple<CallSlotData>(node.ParentSlot);
						if (callData.Args.Length != 2)
						{
							throw new InvalidOperationException("Invalid if statement");
						}
						var ifData = new IfSlotData(Condition: callData.Args[0], Body: callData.Args[1]);
						// var ifSlot = table.Add(node.ParentSlot, TableSlotType.If, ifData, log);
						table.ReplaceData(node.ParentSlot, TableSlotType.If, ifData, log);
						table.ReplaceData(slot, TableSlotType.Unknown, InvalidSlotData.Instance, log); // invalidate id slot
						break; 
					}

					log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}0000 = ROOT");
					break;
				}
				currentNode = table.GetSlot(currentSlot);

				if (currentNode.DataType == TableSlotType.Braces)
				{
					var bracesData = (BracesSlotData)currentNode.Data;
					if (bracesData.TryGetNameTableValue(ident, out var valueSlot))
					{
						log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}{currentSlot:0000} FOUND: {bracesData}");
						var newData = slotData with { Target = valueSlot };
						table.UpdateData(slot, newData, log);
						break;
					}
				}
				else
				{
					log.WriteLine($"{String.Concat(Enumerable.Repeat("  ", indent))}{currentSlot:0000} = {currentNode.DataType}");

				}
			}
		}
	}

	private static void BuildTypes(Table table, TextWriter log)
	{
		// updated nodes that trigger its dependents to refresh
		var sourceQueue = new Queue<int>();

		foreach (var (slot, node) in table.All.Index())
		{
			switch (node.DataType)
			{
				case TableSlotType.Integer:
				{
					var intType = table.GetOrAddType(new NativeTypeSlot("int"), log);
					table.UpdateType(slot, intType, log);
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					break;
				}
				case TableSlotType.Intrinsic:
				{
					sourceQueue.Enqueue(slot);
					log.WriteLine($"enqueue {slot:0000}");
					log.WriteLine($"intrinsic: {slot:0000} = {node}");
					// TODO: intrinsic types
					break;
				}
				case TableSlotType.Parameter:
				{
					var intType = table.GetOrAddType(ParameterTypeSlot.Instance, log);
					table.UpdateType(slot, intType, log);
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
			foreach (var (targetSlotId, targetSlot) in table.All.Index())
			{
				if (targetSlot.DataType == TableSlotType.Declare)
				{
					var declareData = table.GetSlotData<DeclareSlotData>(targetSlotId);

					if (targetSlot.TypeSlot != 0) { continue; } // already set

					if (declareData.Type == sourceSlotId)
					{
						var (s2, t2) = table.GetSlotTuple<IntrinsicSlotData>(declareData.Type);

						var t3 = table.GetTypeSlot(s2.TypeSlot);
						if (t3 is MetaTypeSlot { InstanceType: { } instanceType2 })
						{
							table.UpdateType(targetSlotId, instanceType2, log);
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
							var valueSlot = table.GetSlot(valueSlotId);
							var valueSlotType = valueSlot.TypeSlot;

							if (valueSlotType != targetSlot.TypeSlot)
							{
								table.UpdateType(targetSlotId, valueSlotType, log);
								sourceQueue.Enqueue(targetSlotId);
							}
						}
					}
				}
				else if (targetSlot.DataType == TableSlotType.Add)
				{
					// NOTE: this assumes that add produces the same type as its operands

					var (addSlot, addData) = table.GetSlotTuple<AddOpSlotData>(targetSlotId);

					// skip if either operand is not known yet
					if (addData.Left == 0 || addData.Right == 0)
					{
						continue;
					}

					var leftType = table.GetSlot(addData.Left).TypeSlot;
					var rightType = table.GetSlot(addData.Right).TypeSlot;

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

					table.UpdateType(targetSlotId, leftType, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.DataType == TableSlotType.Identifier)
				{
					var (idSlot, idData) = table.GetSlotTuple<IdentifierSlotData>(targetSlotId);

					if (idData.Target != sourceSlotId)
					{
						continue;
					}

					log.WriteLine($"id target: {targetSlotId}");

					var sourceSlotRecord = table.GetSlot(sourceSlotId);
					if (sourceSlotRecord.TypeSlot == 0) { continue; } // should be redundant

					var typeSlot = table.GetTypeSlot(sourceSlotRecord.TypeSlot);
					if (typeSlot is FunctionTypeSlot)
					{
						// identifier to a function must turn into a call
						var parentSlot = table.GetSlot(idSlot.ParentSlot);
						if (parentSlot.DataType != TableSlotType.Call)
						{
							var newIdSlotId = table.Add(targetSlotId, TableSlotType.Identifier, idData, log);
							table.UpdateType(newIdSlotId, sourceSlotRecord.TypeSlot, log);
							table.ReplaceData(targetSlotId, TableSlotType.Call, new CallSlotData(Target: newIdSlotId, Args: []), log);
							sourceQueue.Enqueue(newIdSlotId);
							sourceQueue.Enqueue(targetSlotId);
							continue;
						}
					}

					table.UpdateType(targetSlotId, sourceSlotRecord.TypeSlot, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.DataType == TableSlotType.Return)
				{
					var (returnSlot, returnData) = table.GetSlotTuple<ReturnSlotData>(targetSlotId);

					if (returnData.Value != sourceSlotId)
					{
						continue;
					}

					var sourceTypeId = table.GetSlot(sourceSlotId).TypeSlot;
					var typeRow = table.GetTypeSlot(sourceTypeId);
					log.WriteLine($"slot {targetSlotId:0000} return: type <- {typeRow} {sourceTypeId} via {returnData.Value:0000}");
					table.UpdateType(targetSlotId, sourceTypeId, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.DataType == TableSlotType.Braces)
				{
					var (bracesSlot, bracesData) = table.GetSlotTuple<BracesSlotData>(targetSlotId);

					if (bracesSlot.TypeSlot != 0) { continue; } // already found a return value, but might need to consider more?

					var returnSlot = table.GetSlot(sourceSlotId);
					if (returnSlot.DataType != TableSlotType.Return) { continue; } // should be redundant
					var returnData = (ReturnSlotData)returnSlot.Data;
					if (returnData.Function != targetSlotId) { continue; } // should be redundant
					if (returnSlot.TypeSlot == 0) { continue; } // should be redundant

					var returnType = table.GetTypeSlot(returnSlot.TypeSlot);
					var funcType = new FunctionTypeSlot(returnSlot.TypeSlot);
					var funcTypeId = table.GetOrAddType(funcType, log);

					log.WriteLine($"slot {targetSlotId:0000} braces: found return {sourceSlotId:0000} {funcTypeId}");
					table.UpdateType(targetSlotId, funcTypeId, log);
					sourceQueue.Enqueue(targetSlotId);
				}
				else if (targetSlot.DataType == TableSlotType.Call)
				{
					var (callSlot, callData) = table.GetSlotTuple<CallSlotData>(targetSlotId);
					if (callSlot.TypeSlot != 0) { continue; } // already set

					// match dequeued source slot
					if (callData.Target != sourceSlotId && !callData.Args.Contains(sourceSlotId)) { continue; }
					var callTargetSlot = table.GetSlot(callData.Target);

					// skip if target or arg types are not known yet
					if (callTargetSlot.TypeSlot == 0) { continue; }
					if (callData.Args.Any(i => table.GetSlot(i).TypeSlot == 0)) { continue; }

					var callTargetType = table.GetTypeSlot(callTargetSlot.TypeSlot);
					if (callTargetType is not FunctionTypeSlot { ReturnType: var returnType }) { throw new InvalidOperationException($"Invalid call target type: {callTargetType}"); }

					table.UpdateType(targetSlotId, returnType, log);
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

	private static int BuildDeclareNode(Boolean isStatic, int parentSlot, Table table, TextWriter log, SyntaxNode idNode, SyntaxNode? typeNode, SyntaxNode valueNode)
	{
		if (idNode is not Parse.IdentifierNode { Text: { Length: > 0 } id })
		{
			throw new Exception("Invalid identifier");
		}

		// TODO: add slot, enqueue child nodes, queue update slot with analyzed data
		var data = new DeclareSlotData(Name: id, IsStatic: isStatic);
		var slot = table.Add(parentSlot, TableSlotType.Declare, data, log);

		// Name
		if (!table.TryGetSlot<BracesSlotData>(parentSlot, TableSlotType.Braces, out var bracesData, log))
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
			typeSlot = BuildTable(typeNode, slot, table, log);
			data = data with { Type = typeSlot };
			table.UpdateData(slot, data, log);
		}

		// Value
		var valueSlot = BuildTable(valueNode, slot, table, log);
		data = data with { Value = valueSlot };
		table.UpdateData(slot, data, log);

		return slot;
	}
}