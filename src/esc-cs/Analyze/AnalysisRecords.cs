using System.Diagnostics.CodeAnalysis;

namespace EscLang.Analyze;

public record class Analysis(Scope Main)
{
}

public record class Scope()
{
	public Scope? Parent;
	public Dictionary<String, Type?> NameTable = [];
	public List<Step> Steps = [];
	public Boolean TryGetNameTableValue(String Identifier, [MaybeNullWhen(false)] out Type? Value)
	{
		if (NameTable.TryGetValue(Identifier, out Value))
		{
			return true;
		}
		if (Parent is not null)
		{
			return Parent.TryGetNameTableValue(Identifier, out Value);
		}
		return false;
	}
}

public abstract record class Step;
public record class DeclareStep(Scope Parent, String Identifier, TypedExpression Value, Boolean IsStatic) : Step;
public record class PrintStep(Scope Parent, TypedExpression Value) : Step;
public record class ReturnStep(Scope Parent, TypedExpression Value) : Step;
public record class ExpressionStep(Scope Parent, TypedExpression Value) : Step;

public abstract record class TypedExpression(Type Type);
public record class KeywordExpression(String Keyword) : TypedExpression(typeof(void));
public record class IntLiteralExpression(Int32 Value) : TypedExpression(typeof(Int32));
public record class StringLiteralExpression(String Value) : TypedExpression(typeof(String));
public record class IdentifierExpression(Type Type, String Identifier) : TypedExpression(Type);
public record class AddExpression(Type Type, TypedExpression Left, TypedExpression Right) : TypedExpression(Type);
public record class FunctionScopeExpression(Scope Scope) : TypedExpression(typeof(FunctionScopeExpression));
public record class MemberMethodGroupExpression(TypedExpression Target, String MethodName) : TypedExpression(typeof(void)); // actual type depends on method selection
public record class CallExpression(Type ReturnType, System.Reflection.MethodInfo MethodInfo, TypedExpression Target, TypedExpression[] Args) : TypedExpression(ReturnType);
public record class AssignExpression(Type Type, TypedExpression Target, TypedExpression Value) : TypedExpression(Type);
