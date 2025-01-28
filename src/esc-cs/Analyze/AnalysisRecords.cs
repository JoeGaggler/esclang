using System.Diagnostics.CodeAnalysis;

namespace EscLang.Analyze;

public record class Analysis(Scope Main)
{
}

public class Table
{
	private readonly List<TableSlot> Slots = [new(0, TableSlotType.Unknown, InvalidSlotData.Instance)];
	private readonly List<TypeSlot> Types = [UnknownTypeSlot.Instance];

	public Table()
	{
	}

	public void UpdateType(int slotId, int typeSlotId, TextWriter? log)
	{
		log ??= TextWriter.Null;
		var slot = Slots[slotId] with { TypeSlot = typeSlotId };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} <- {slot.DataType} : {typeSlotId} = {slot.Data}");
	}

	public int GetOrAddType(TypeSlot type, TextWriter log)
	{
		// TODO: linear search could be slow
		var id = -1;
		id = Types.IndexOf(type);
		if (id == -1)
		{
			id = Types.Count;
			Types.Add(type);
			log.WriteLine($"add type {id} = {type}");
		}
		return id;
	}

	public int Add(int parentSlot, TableSlotType type, SlotData data, TextWriter log)
	{
		var id = Slots.Count;
		var slot = new TableSlot(parentSlot, type, data);
		Slots.Add(slot);
		log.WriteLine($"slot {id:0000} in {parentSlot:0000} :: {type} = {data}");
		return id;
	}

	public void UpdateData(int slotId, SlotData data, TextWriter log)
	{
		var slot = Slots[slotId] with { Data = data };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} <- {slot.DataType} = {data}");
	}

	public void ReplaceData(int slotId, TableSlotType type, SlotData data, TextWriter log)
	{
		// TODO: replacing a slot may invalidate previously referenced slots that are no longer reachable, caller should try to avoid this situation by marking the slots as invalid
		var slot = Slots[slotId] with { DataType = type, Data = data };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} << {slot.DataType} = {data}");
	}

	public bool TryGetSlot<T>(int slotId, TableSlotType type, [MaybeNullWhen(false)] out T dataIfFound, TextWriter log) where T : SlotData
	{
		var slot = Slots[slotId];
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} -> {slot.DataType} = {slot.Data}");
		if (slot.DataType != type)
		{
			dataIfFound = default;
			return false;
		}

		dataIfFound = slot.Data as T;
		return dataIfFound is not null;
	}

	public IEnumerable<TableSlot> All
	{
		get
		{
			for (var i = 0; i < Slots.Count; i++)
			{
				yield return Slots[i];
			}
		}
	}

	public TableSlot Root { get => Slots[1]; }

	// Instance
	public TableSlot GetSlot(int slotId) => Slots[slotId];
	public (TableSlot, T) GetSlotTuple<T>(int slotId) where T : SlotData => (Slots[slotId], (T)Slots[slotId].Data);
	public T GetSlotData<T>(int slotId) where T : SlotData => (T)Slots[slotId].Data;
	public TypeSlot GetTypeSlot(int typeSlotId) => Types[typeSlotId];
}

public abstract record class TypeSlot;
public record class UnknownTypeSlot : TypeSlot { public static readonly UnknownTypeSlot Instance = new(); private UnknownTypeSlot() { } }
public record class VoidTypeSlot : TypeSlot { public static readonly VoidTypeSlot Instance = new(); private VoidTypeSlot() { } }
public record class ParameterTypeSlot : TypeSlot { public static readonly ParameterTypeSlot Instance = new(); private ParameterTypeSlot() { } }
public record class MetaTypeSlot(int InstanceType) : TypeSlot;
public record class NativeTypeSlot(String Name) : TypeSlot;
public record class FunctionTypeSlot(int ReturnType) : TypeSlot;

public enum TableSlotType
{
	Unknown = 0,
	File,
	Declare,
	Call,
	Identifier,
	Braces,
	Integer,
	String,
	Add,
	Return,
	Intrinsic,
	Parameter,
	LogicalNegation,
}

public record class TableSlot(int ParentSlot, TableSlotType DataType, SlotData Data, int TypeSlot = 0)
{

}

public abstract record class SlotData;
public record class InvalidSlotData : SlotData { public static readonly InvalidSlotData Instance = new(); private InvalidSlotData() { } }
public record class FileSlotData(int Main = 0) : SlotData;
public record class DeclareSlotData(String Name, Boolean IsStatic, int Type = 0, int Value = 0) : SlotData;
public record class CallSlotData(int Target, int[] Args) : SlotData;
public record class IdentifierSlotData(String Name, int Target = 0) : SlotData;
public record class IntrinsicSlotData(String Name) : SlotData;
public record class BracesSlotData(int[] Lines) : SlotData
{
	private readonly Dictionary<String, int> NameTable = [];

	public Boolean TryAddNameTableValue(String name, int slot)
	{
		if (NameTable.ContainsKey(name))
		{
			return false;
		}
		NameTable.Add(name, slot);
		return true;
	}

	public Boolean TryGetNameTableValue(String name, [MaybeNullWhen(false)] out int slot)
	{
		return NameTable.TryGetValue(name, out slot);
	}
}
public record class IntegerSlotData(Int32 Value) : SlotData;
public record class StringSlotData(String Value) : SlotData;
public record class AddOpSlotData(Int32 Left = 0, Int32 Right = 0) : SlotData;
public record class ReturnSlotData(int Value = 0, int Function = 0) : SlotData;
public record class ParameterSlotData : SlotData;
public record class LogicalNegationSlotData(int Value = 0) : SlotData;

////////////////////////////////

public record class Scope(Int32 Id)
{
	public Scope? Parent;
	public Dictionary<String, AnalysisType?> NameTable = [];
	public List<TypedExpression> Expressions = [];
	public Boolean TryGetNameTableValue(String Identifier, [MaybeNullWhen(false)] out AnalysisType? Value)
	{
		if (NameTable.TryGetValue(Identifier, out Value))
		{
			return true;
		}
		if (Parent is not null)
		{
			return Parent.TryGetNameTableValue(Identifier, out Value);
		}
		return false;
	}
}

public abstract record class AnalysisType
{
	public abstract String FullName { get; } // Only used for debugging, could be extracted elsewhere
	public static readonly AnalysisType String = new DotnetAnalysisType(typeof(String));
};
public record class UnknownAnalysisType() : AnalysisType
{
	public static readonly UnknownAnalysisType Instance = new();

	public override String FullName => "Unknown";
}
public record class VoidAnalysisType() : AnalysisType
{
	public static readonly VoidAnalysisType Instance = new();

	public override String FullName => "Void";
}
public record class FunctionAnalysisType(AnalysisType ReturnType) : AnalysisType
{
	public override String FullName => "Function";
}
public record class DotnetAnalysisType(Type Type) : AnalysisType
{
	public override String FullName => $"Dotnet::{Type.FullName}";
}

public abstract record class TypedExpression(AnalysisType Type);
public record class FileExpression(AnalysisType Type) : TypedExpression(Type);
public record class KeywordExpression(String Keyword) : TypedExpression(UnknownAnalysisType.Instance);
public record class IntrinsicFunctionExpression(String Name, AnalysisType Type) : TypedExpression(Type);
public record class VoidExpression() : TypedExpression(VoidAnalysisType.Instance) { public static readonly VoidExpression Instance = new(); }
public record class ReturnValueExpression(TypedExpression ReturnValue) : TypedExpression(ReturnValue.Type) { public static readonly ReturnValueExpression VoidInstance = new ReturnValueExpression(VoidExpression.Instance); }
public record class IntLiteralExpression(Int32 Value) : TypedExpression(new DotnetAnalysisType(typeof(Int32)));
public record class StringLiteralExpression(String Value) : TypedExpression(new DotnetAnalysisType(typeof(String)));
public record class BooleanLiteralExpression(Boolean Value) : TypedExpression(new DotnetAnalysisType(typeof(Boolean)));
public record class IdentifierExpression(AnalysisType Type, String Identifier) : TypedExpression(Type);
public record class DeclarationExpression(AnalysisType Type, String Identifier, TypedExpression Value, Boolean IsStatic) : TypedExpression(Type);
public record class AddExpression(AnalysisType Type, TypedExpression Left, TypedExpression Right) : TypedExpression(Type);
public record class FunctionExpression(Scope Scope, AnalysisType ReturnType) : TypedExpression(new FunctionAnalysisType(ReturnType));
public record class MemberExpression(AnalysisType Type, TypedExpression Target, String MemberName) : TypedExpression(Type);
public record class DotnetMemberMethodExpression(AnalysisType ReturnType, System.Reflection.MethodInfo MethodInfo, TypedExpression Target) : TypedExpression(ReturnType);
public record class CallExpression(AnalysisType ReturnType, TypedExpression Target, TypedExpression[] Args) : TypedExpression(ReturnType);
public record class ParameterExpression(AnalysisType Type) : TypedExpression(Type: Type);
public record class AssignExpression(AnalysisType Type, TypedExpression Target, TypedExpression Value) : TypedExpression(Type);
public record class LogicalNegationExpression(TypedExpression Node) : TypedExpression(new DotnetAnalysisType(typeof(Boolean)));
