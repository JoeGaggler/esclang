using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EscLang.Analyze;

public class Analysis
{
	private readonly List<CodeSlot> CodeSlots = [new(0, CodeSlotEnum.Unknown, InvalidCodeData.Instance)];

	public Analysis()
	{
	}

	public void UpdateType(int slotId, int typeSlotId2, TextWriter? log = null)
	{
		log ??= TextWriter.Null;
		var slot = CodeSlots[slotId] with { TypeSlot2 = typeSlotId2 };
		CodeSlots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} <- {slot.CodeType} : ({typeSlotId2:0000}) = {slot.Data}");
	}

	public int GetOrAddType2(TypeCodeData typeCodeData, TextWriter log)
	{
		foreach (var (i, slot) in CodeSlots.Index())
		{
			if (slot.CodeType != CodeSlotEnum.Type) { continue; }
			if (slot.Data == typeCodeData) { return i; }
		}
		return Add(Analyzer.FAKE_GLOBAL_PARENT, CodeSlotEnum.Type, typeCodeData, log);
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
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} <- {slot.CodeType} = {data}");
	}

	public void ReplaceData(int slotId, CodeSlotEnum type, CodeData data, TextWriter log)
	{
		// TODO: replacing a slot may invalidate previously referenced slots that are no longer reachable, caller should try to avoid this situation by marking the slots as invalid
		var slot = CodeSlots[slotId] with { CodeType = type, Data = data };
		CodeSlots[slotId] = slot;
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} << {slot.CodeType} = {data}");
	}

	public bool TryGetSlot<T>(int slotId, CodeSlotEnum type, [MaybeNullWhen(false)] out T dataIfFound, TextWriter log) where T : CodeData
	{
		var slot = CodeSlots[slotId];
		log.WriteLine($"slot {slotId:0000} in {slot.Parent:0000} -> {slot.CodeType} = {slot.Data}");
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
}

[Obsolete]
public abstract record class TypeData;
[Obsolete]
public record class UnknownTypeData : TypeData { public static readonly UnknownTypeData Instance = new(); private UnknownTypeData() { } }
[Obsolete]
public record class TypeTypeData : TypeData { public static readonly TypeTypeData Instance = new(); private TypeTypeData() { } }
[Obsolete]
public record class VoidTypeData : TypeData { public static readonly VoidTypeData Instance = new(); private VoidTypeData() { } }
[Obsolete]
public record class ParameterTypeData : TypeData { public static readonly ParameterTypeData Instance = new(); private ParameterTypeData() { } }
// public record class MetaTypeData(int Type) : TypeData;
[Obsolete]
public record class FunctionTypeData(int ReturnType, int ReturnType2) : TypeData;
[Obsolete]
public record class DotnetMemberTypeData(int TargetType, String MemberName, MemberTypes MemberType, MemberInfo[] Members) : TypeData;
[Obsolete]
public record class DotnetTypeData(Type Type) : TypeData;

public enum CodeSlotEnum
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

public record class CodeSlot(int Parent, CodeSlotEnum CodeType, CodeData Data, int TypeSlot2 = 0);

public abstract record class CodeData;
public record class InvalidCodeData : CodeData { public static readonly InvalidCodeData Instance = new(); private InvalidCodeData() { } }
public record class FileCodeData(int Main = 0) : CodeData;
public abstract record class TypeCodeData : CodeData;
public record class SomeTypeCodeData(String Name) : TypeCodeData;
public record class FuncTypeCodeData(int ReturnType) : TypeCodeData;
public record class DotnetTypeCodeData(Type Type) : TypeCodeData;
public record class DotnetMemberTypeCodeData(int TargetType, String MemberName, MemberTypes MemberType, MemberInfo[] Members) : TypeCodeData;
public record class DeclareCodeData(String Name, Boolean IsStatic, int Type = 0, int Value = 0) : CodeData;
public record class CallCodeData(int Target, int[] Args, MethodInfo? DotnetMethod = null) : CodeData;
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
public record class VoidCodeData : CodeData { public static readonly VoidCodeData Instance = new(); private VoidCodeData() { } }
public record class BooleanCodeData(Boolean Value) : CodeData;
public record class IntegerCodeData(Int32 Value) : CodeData;
public record class StringCodeData(String Value) : CodeData;
public record class AddOpCodeData(Int32 Left = 0, Int32 Right = 0) : CodeData;
public record class AssignCodeData(Int32 Target = 0, Int32 Value = 0) : CodeData;
public record class MemberCodeData(Int32 Target, Int32 Member) : CodeData;
public record class ReturnCodeData(int Value = 0, int Function = 0) : CodeData;
public record class ParameterCodeData : CodeData;
public record class LogicalNegationCodeData(int Value = 0) : CodeData;
public record class NegationCodeData(int Value = 0) : CodeData;