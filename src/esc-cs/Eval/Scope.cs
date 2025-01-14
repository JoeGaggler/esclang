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

	public void Set(String identifier, ExpressionResult value)
	{
		this.Store[identifier] = value;
	}
}

public abstract record class ExpressionResult;
public record class ImplicitVoidExpressionResult() : ExpressionResult;
public record class ObjectExpressionResult(Object Value) : ExpressionResult;
public record class IntExpressionResult(Int32 Value) : ExpressionResult;
public record class StringExpressionResult(String Value) : ExpressionResult;
public record class FunctionDeclarationExpressionResult() : ExpressionResult; // TODO: inputs/outputs
public record class ReturnVoidResult() : ExpressionResult;
public record class ReturnExpressionResult(ExpressionResult Value) : ExpressionResult;