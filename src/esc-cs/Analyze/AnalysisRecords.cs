using System.Diagnostics.CodeAnalysis;

namespace EscLang.Analyze;

public record class Analysis(Scope Main)
{
}

public record class Scope()
{
	public Scope? Parent;
	public Dictionary<String, AnalysisType?> NameTable = [];
	public List<Step> Steps = [];
	public Boolean TryGetNameTableValue(String Identifier, [MaybeNullWhen(false)] out AnalysisType? Value)
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

public abstract record class AnalysisType
{
	public abstract String FullName { get; }
};
public record class UnknownAnalysisType() : AnalysisType
{
	public static UnknownAnalysisType Instance = new();
	public override String FullName => "Unknown";
}
public record class FunctionAnalysisType(AnalysisType ReturnType) : AnalysisType
{
	public static UnknownAnalysisType Instance = new();
	public override String FullName => "Function";
}
public record class DotnetAnalysisType(Type Type) : AnalysisType
{
	public override String FullName => $"Dotnet::{Type.FullName}";
}

public abstract record class Step;
public record class DeclareStep(Scope Parent, String Identifier, TypedExpression Value, Boolean IsStatic) : Step;
public record class ExpressionStep(Scope Parent, TypedExpression Value) : Step;

public abstract record class TypedExpression(AnalysisType Type);
public record class KeywordExpression(String Keyword) : TypedExpression(UnknownAnalysisType.Instance);
public record class IntrinsicFunctionExpression(String Name, AnalysisType Type) : TypedExpression(Type);
public record class ReturnExpression(TypedExpression ReturnValue) : TypedExpression(ReturnValue.Type);
public record class IntLiteralExpression(Int32 Value) : TypedExpression(new DotnetAnalysisType(typeof(Int32)));
public record class StringLiteralExpression(String Value) : TypedExpression(new DotnetAnalysisType(typeof(String)));
public record class BooleanLiteralExpression(Boolean Value) : TypedExpression(new DotnetAnalysisType(typeof(Boolean)));
public record class IdentifierExpression(AnalysisType Type, String Identifier) : TypedExpression(Type);
public record class AddExpression(AnalysisType Type, TypedExpression Left, TypedExpression Right) : TypedExpression(Type);
public record class FunctionExpression(Scope Scope, AnalysisType ReturnType) : TypedExpression(new FunctionAnalysisType(ReturnType));
public record class MemberMethodGroupExpression(TypedExpression Target, String MethodName) : TypedExpression(UnknownAnalysisType.Instance); // actual type depends on method selection
public record class CallDotnetMethodExpression(AnalysisType ReturnType, System.Reflection.MethodInfo MethodInfo, TypedExpression Target, TypedExpression[] Args) : TypedExpression(ReturnType);
public record class CallExpression(AnalysisType ReturnType, TypedExpression Target, TypedExpression[] Args) : TypedExpression(ReturnType);
public record class ParameterExpression() : TypedExpression(Type: UnknownAnalysisType.Instance); // depends on usage
public record class AssignExpression(AnalysisType Type, TypedExpression Target, TypedExpression Value) : TypedExpression(Type);
public record class LogicalNegationExpression(TypedExpression Node) : TypedExpression(new DotnetAnalysisType(typeof(Boolean)));
