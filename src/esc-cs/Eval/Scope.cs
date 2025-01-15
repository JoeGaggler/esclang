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

	public Dictionary<String, ExpressionResult> Store { get; } = new();

	public ValueTable()
	{
		this.Parent = null;
	}

	public ValueTable(ValueTable? parent)
	{
		this.Parent = parent;
	}

	private Int32 NextParameterIndex = 0;
	public List<ExpressionResult> Parameters { get; } = new();
	public ExpressionResult GetNextParameter()
	{
		if (NextParameterIndex >= this.Parameters.Count)
		{
			throw new Exception("Too many parameters");
		}
		return this.Parameters[NextParameterIndex++];
	}

	public ExpressionResult? Get(String identifier)
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

	public void Add(String identifier, ExpressionResult value)
	{
		if (this.Store.ContainsKey(identifier))
		{
			throw new Exception($"Duplicate identifier: {identifier}");
		}
		this.Store.Add(identifier, value);
	}

	public void Set(String identifier, ExpressionResult value)
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

public abstract record class ExpressionResult;
public record class ImplicitVoidExpressionResult() : ExpressionResult;
public record class ObjectExpressionResult(Object Value) : ExpressionResult;
public record class IntExpressionResult(Int32 Value) : ExpressionResult;
public record class StringExpressionResult(String Value) : ExpressionResult;
public record class BooleanExpressionResult(Boolean Value) : ExpressionResult;
public record class FunctionDeclarationExpressionResult() : ExpressionResult; // TODO: inputs/outputs
public record class ReturnVoidResult() : ExpressionResult;
public record class ReturnExpressionResult(ExpressionResult Value) : ExpressionResult;