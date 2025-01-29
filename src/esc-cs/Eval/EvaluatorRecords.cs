using EscLang.Parse;

namespace EscLang.Eval;

public class Scope
{
	public Scope? Parent { get; }

	public Dictionary<String, SyntaxNode> Store { get; } = new();

	public Scope()
	{
		this.Parent = null;
	}

	public Scope(Scope? parent)
	{
		this.Parent = parent;
	}

	public SyntaxNode? Get(String identifier)
	{
		if (this.Store.TryGetValue(identifier, out var value))
		{
			return value;
		}
		else if (this.Parent is not null)
		{
			return this.Parent.Get(identifier);
		}
		else
		{
			return null;
		}
	}
}

public class ValueTable
{
	public ValueTable? Parent { get; }

	public Dictionary<String, Evaluation> Store { get; } = new();

	public ValueTable()
	{
		this.Parent = null;
	}

	public ValueTable(ValueTable? parent)
	{
		this.Parent = parent;
	}

	private Int32 NextArgumentIndex = 0;
	public List<Evaluation> Arguments { get; } = new();
	public void SetArguments(Evaluation[] parameters)
	{
		this.Arguments.Clear();
		this.Arguments.AddRange(parameters);
		this.NextArgumentIndex = 0;
	}
	public Evaluation GetNextParameter()
	{
		if (NextArgumentIndex >= this.Arguments.Count)
		{
			throw new Exception($"Function defines more parameters than arguments ({this.Arguments.Count})");
		}
		return this.Arguments[NextArgumentIndex++];
	}

	public Evaluation? Get(String identifier)
	{
		if (this.Store.TryGetValue(identifier, out var value))
		{
			return value;
		}
		else if (this.Parent is not null)
		{
			return this.Parent.Get(identifier);
		}
		else
		{
			return null; // TODO: undefined?
		}
	}

	public void Add(String identifier, Evaluation value)
	{
		if (this.Store.ContainsKey(identifier))
		{
			throw new Exception($"Duplicate identifier: {identifier}");
		}
		this.Store.Add(identifier, value);
	}

	public void Set(String identifier, Evaluation value)
	{
		if (this.Store.ContainsKey(identifier))
		{
			this.Store[identifier] = value;
		}
		else if (this.Parent is not null)
		{
			this.Parent.Set(identifier, value);
		}
		else
		{
			throw new Exception($"Undefined variable: {identifier}");
		}
	}
}

public abstract record class Evaluation;
public record class IntrinsicFunctionEvaluation(String Name) : Evaluation;
public record class VoidEvaluation : Evaluation { public static readonly VoidEvaluation Instance = new(); private VoidEvaluation() { } };
public record class ObjectEvaluation(Object Value) : Evaluation;
public record class IntEvaluation(Int32 Value) : Evaluation;
public record class StringEvaluation(String Value) : Evaluation;
public record class BooleanEvaluation(Boolean Value) : Evaluation;
public record class FunctionDeclarationEvaluation() : Evaluation; // TODO: inputs/outputs
public record class DotnetMemberMethodEvaluation(System.Reflection.MethodInfo MethodInfo, Evaluation Target) : Evaluation;
public record class ReturnVoidEvaluation : Evaluation { public static readonly ReturnVoidEvaluation Instance = new(); private ReturnVoidEvaluation() { } };
public record class ReturnValueEvaluation(Evaluation Value) : Evaluation;
public record class FunctionEvaluation(int BracesSlotId) : Evaluation;
public record class MemberEvaluation(Evaluation Target, String Name, System.Reflection.MemberInfo[] Member) : Evaluation;