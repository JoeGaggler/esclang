using System.Diagnostics.CodeAnalysis;

namespace EscLang.Analyze;

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
