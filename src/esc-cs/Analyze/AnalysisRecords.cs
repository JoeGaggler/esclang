using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EscLang.Analyze;

public class Analysis
{
	private readonly List<CodeSlot> CodeSlots = [new(0, CodeSlotEnum.Unknown, InvalidCodeData.Instance)];
	private readonly List<TypeData> TypeSlots = [UnknownTypeData.Instance];

	public Analysis()
	{
	}

	public void UpdateType(int slotId, int typeSlotId, TextWriter? log)
	{
		log ??= TextWriter.Null;
		var slot = CodeSlots[slotId] with { TypeSlot = typeSlotId };
		CodeSlots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} <- {slot.CodeType} : {typeSlotId} = {slot.Data}");
	}

	public int GetOrAddType(TypeData type, TextWriter log)
	{
		// TODO: linear search could be slow
		var id = -1;
		id = TypeSlots.IndexOf(type);
		if (id == -1)
		{
			id = TypeSlots.Count;
			TypeSlots.Add(type);
			log.WriteLine($"add type {id} = {type}");
		}
		return id;
	}

	public int Add(int parentSlot, CodeSlotEnum type, CodeData data, TextWriter log)
	{
		var id = CodeSlots.Count;
		var slot = new CodeSlot(parentSlot, type, data);
		CodeSlots.Add(slot);
		log.WriteLine($"slot {id:0000} in {parentSlot:0000} :: {type} = {data}");
		return id;
	}

	public void UpdateData(int slotId, CodeData data, TextWriter log)
	{
		var slot = CodeSlots[slotId] with { Data = data };
		CodeSlots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} <- {slot.CodeType} = {data}");
	}

	public void ReplaceData(int slotId, CodeSlotEnum type, CodeData data, TextWriter log)
	{
		// TODO: replacing a slot may invalidate previously referenced slots that are no longer reachable, caller should try to avoid this situation by marking the slots as invalid
		var slot = CodeSlots[slotId] with { CodeType = type, Data = data };
		CodeSlots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} << {slot.CodeType} = {data}");
	}

	public bool TryGetSlot<T>(int slotId, CodeSlotEnum type, [MaybeNullWhen(false)] out T dataIfFound, TextWriter log) where T : CodeData
	{
		var slot = CodeSlots[slotId];
		log.WriteLine($"slot {slotId:0000} in {slot.ParentSlot:0000} -> {slot.CodeType} = {slot.Data}");
		if (slot.CodeType != type)
		{
			dataIfFound = default;
			return false;
		}

		dataIfFound = slot.Data as T;
		return dataIfFound is not null;
	}

	public IEnumerable<CodeSlot> All
	{
		get
		{
			for (var i = 0; i < CodeSlots.Count; i++)
			{
				yield return CodeSlots[i];
			}
		}
	}

	// Instance
	public CodeSlot GetCodeSlot(int slotId) => CodeSlots[slotId];
	public T GetCodeData<T>(int slotId) where T : CodeData => (T)CodeSlots[slotId].Data;
	public TypeData GetTypeData(int typeSlotId) => TypeSlots[typeSlotId];
}

public abstract record class TypeData;
public record class UnknownTypeData : TypeData { public static readonly UnknownTypeData Instance = new(); private UnknownTypeData() { } }
public record class VoidTypeData : TypeData { public static readonly VoidTypeData Instance = new(); private VoidTypeData() { } }
public record class ParameterTypeData : TypeData { public static readonly ParameterTypeData Instance = new(); private ParameterTypeData() { } }
public record class MetaTypeData(int InstanceType) : TypeData;
public record class NativeTypeData(String Name) : TypeData;
public record class FunctionTypeData(int ReturnType) : TypeData;
public record class MemberTypeData(int TargetType) : TypeData;
// TODO: public record class MethodTypeData(int TargetType) : TypeData;
public record class DotnetTypeData(Type Type) : TypeData;

public enum CodeSlotEnum
{
	Unknown = 0,
	File,
	Declare,
	Call,
	Identifier,
	Braces,
	Boolean,
	Integer,
	String,
	If,
	Add,
	Return,
	Intrinsic,
	Parameter,
	LogicalNegation,
	Assign,
	Member,
}

public record class CodeSlot(int ParentSlot, CodeSlotEnum CodeType, CodeData Data, int TypeSlot = 0);

public abstract record class CodeData;
public record class InvalidCodeData : CodeData { public static readonly InvalidCodeData Instance = new(); private InvalidCodeData() { } }
public record class FileCodeData(int Main = 0) : CodeData;
public record class DeclareCodeData(String Name, Boolean IsStatic, int Type = 0, int Value = 0) : CodeData;
public record class CallCodeData(int Target, int[] Args) : CodeData;
public record class IdentifierCodeData(String Name, int Target = 0) : CodeData;
public record class IntrinsicCodeData(String Name) : CodeData;
public record class IfSlotCodeData(int Condition, int Body) : CodeData;
public record class BracesCodeData(int[] Lines) : CodeData
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
public record class BooleanCodeData(Boolean Value) : CodeData;
public record class IntegerCodeData(Int32 Value) : CodeData;
public record class StringCodeData(String Value) : CodeData;
public record class AddOpCodeData(Int32 Left = 0, Int32 Right = 0) : CodeData;
public record class AssignCodeData(Int32 Target = 0, Int32 Value = 0) : CodeData;
public record class MemberCodeData(Int32 Target, Int32 Member, MemberInfo[] Members) : CodeData;
public record class ReturnCodeData(int Value = 0, int Function = 0) : CodeData;
public record class ParameterCodeData : CodeData;
public record class LogicalNegationCodeData(int Value = 0) : CodeData;
