using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EscLang.Analyze;

public class Analysis
{
	private readonly List<Slot> Slots = [new(0, SlotEnum.Unknown, InvalidSlotData.Instance)];

	public Analysis()
	{
	}

	public void UpdateType(int slotId, int typeSlotId2, TextWriter? log = null)
	{
		log ??= TextWriter.Null;
		var slot = Slots[slotId] with { TypeSlot = typeSlotId2 };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} <- {slot.CodeType} : ({typeSlotId2:0000}) = {slot.Data}");
	}

	public int GetOrAddType(TypeSlotData typeCodeData, TextWriter log)
	{
		foreach (var (i, slot) in Slots.Index())
		{
			if (slot.CodeType != SlotEnum.Type) { continue; }
			if (slot.Data == typeCodeData) { return i; }
		}
		var parent = 0; // types are global
		return Add(parent, SlotEnum.Type, typeCodeData, log);
	}

	public int Add(int parentSlot, SlotEnum type, SlotData data, TextWriter log)
	{
		var id = Slots.Count;
		var slot = new Slot(parentSlot, type, data);
		Slots.Add(slot);
		log.WriteLine($"slot {id:0000} in {parentSlot:0000} :: {type} = {data}");
		return id;
	}

	public void UpdateData(int slotId, SlotData data, TextWriter log)
	{
		var slot = Slots[slotId] with { Data = data };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} <- {slot.CodeType} = {data}");
	}

	public void ReplaceData(int slotId, SlotEnum type, SlotData data, TextWriter log)
	{
		// TODO: replacing a slot may invalidate previously referenced slots that are no longer reachable, caller should try to avoid this situation by marking the slots as invalid
		var slot = Slots[slotId] with { CodeType = type, Data = data };
		Slots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} << {slot.CodeType} = {data}");
	}

	public bool TryGetSlot<T>(int slotId, SlotEnum type, [MaybeNullWhen(false)] out T dataIfFound, TextWriter log) where T : SlotData
	{
		var slot = Slots[slotId];
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} -> {slot.CodeType} = {slot.Data}");
		if (slot.CodeType != type)
		{
			dataIfFound = default;
			return false;
		}

		dataIfFound = slot.Data as T;
		return dataIfFound is not null;
	}

	public IEnumerable<Slot> All
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
	public Slot GetSlot(int slotId) => Slots[slotId];
	public T GetData<T>(int slotId) where T : SlotData => (T)Slots[slotId].Data;
}

public enum SlotEnum
{
	Unknown = 0,
	Type,
	File,
	Declare,
	Call,
	Identifier,
	Braces,
	Void,
	Boolean,
	Integer,
	String,
	If,
	Add,
	Return,
	Intrinsic,
	Parameter,
	LogicalNegation,
	Negation,
	Assign,
	Member,
}

public record class Slot(int Parent, SlotEnum CodeType, SlotData Data, int TypeSlot = 0);

public abstract record class SlotData;
public record class InvalidSlotData : SlotData { public static readonly InvalidSlotData Instance = new(); private InvalidSlotData() { } }
public record class FileSlotData(int Main = 0) : SlotData;
public abstract record class TypeSlotData : SlotData;
public record class MetaTypeSlotData(int Type) : TypeSlotData { public static readonly MetaTypeSlotData Root = new(0); }
public record class FuncTypeSlotData(int ReturnType) : TypeSlotData;
public record class DotnetTypeSlotData(Type Type) : TypeSlotData;
public record class DotnetMemberTypeSlotData(int TargetType, String MemberName, MemberTypes MemberType, MemberInfo[] Members) : TypeSlotData;
public record class DeclareSlotData(String Name, Boolean IsStatic, int Type = 0, int Value = 0) : SlotData;
public record class CallSlotData(int Target, int[] Args, MethodInfo? DotnetMethod = null) : SlotData;
public record class IdentifierSlotData(String Name, int Target = 0) : SlotData;
public record class IntrinsicSlotData(String Name) : SlotData;
public record class IfSlotData(int Condition, int Body) : SlotData;
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
public record class VoidSlotData : SlotData { public static readonly VoidSlotData Instance = new(); private VoidSlotData() { } }
public record class BooleanSlotData(Boolean Value) : SlotData;
public record class IntegerSlotData(Int32 Value) : SlotData;
public record class StringSlotData(String Value) : SlotData;
public record class AddSlotData(Int32 Left = 0, Int32 Right = 0) : SlotData;
public record class AssignSlotData(Int32 Target = 0, Int32 Value = 0) : SlotData;
public record class MemberSlotData(Int32 Target, Int32 Member) : SlotData;
public record class ReturnSlotData(int Value = 0, int Function = 0) : SlotData;
public record class ParameterSlotData : SlotData;
public record class LogicalNegationSlotData(int Value = 0) : SlotData;
public record class NegationSlotData(int Value = 0) : SlotData;