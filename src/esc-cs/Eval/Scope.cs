namespace EscLang.Eval;

public class Scope
{
	public Scope? Parent { get; }

	public Dictionary<String, Object> Store { get; } = new();

	public Scope()
	{
		this.Parent = null;
	}

	public Scope(Scope? parent)
	{
		this.Parent = parent;
	}

	public Object? Get(String identifier)
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