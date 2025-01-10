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

public abstract record class TypedExpression;
public record class IntLiteralExpression(Int32 Value) : TypedExpression;
public record class IdentifierExpression(String Identifier) : TypedExpression;
