namespace EscLang.Analyze;

public record class Analysis(Scope Main)
{
}

public record class Scope()
{
	public Scope? Parent;
	public Dictionary<String, Type?> NameTable = [];
	public List<Step> Steps = [];
}

public abstract record class Step;
public record class AssignStep(Scope Parent, String Identifier, TypedExpression Value) : Step;
public record class PrintStep(Scope Parent, TypedExpression Value) : Step;
public record class ReturnStep(Scope Parent, TypedExpression Value) : Step;

public abstract record class TypedExpression(Type Type);
public record class IntLiteralExpression(Int32 Value) : TypedExpression(typeof(Int32));
public record class IdentifierExpression(Type Type, String Identifier) : TypedExpression(Type);
public record class AddExpression(Type Type, TypedExpression Left, TypedExpression Right) : TypedExpression(Type);
public record class FunctionScopeExpression(Scope Scope) : TypedExpression(typeof(FunctionScopeExpression));
