namespace EscLang.Analyze;

public record class Analysis(Scope Main)
{
}

public record class Scope()
{
	public Scope? Parent;
	public HashSet<String> NameTable = [];
	public List<Step> Steps = [];
}

public abstract record class Step;
public record class AssignStep(Scope parent, String Identifier, TypedExpression Value) : Step;
public record class PrintStep(Scope parent, TypedExpression Value) : Step;

public abstract record class TypedExpression(Type Type);
public record class IntLiteralExpression(Int32 Value) : TypedExpression(typeof(Int32));
public record class IdentifierExpression(Type Type, String Identifier) : TypedExpression(Type);
public record class AddExpression(Type Type, TypedExpression Left, TypedExpression Right) : TypedExpression(Type);
