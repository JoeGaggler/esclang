namespace EscLang.Parse;

public record EscFile(List<SyntaxNode> Nodes) { }

public abstract record SyntaxNode { }

public record LiteralCharNode(String Text) : SyntaxNode { }

public record LiteralNumberNode(String Text) : SyntaxNode { }

public record LiteralStringNode(String Text) : SyntaxNode { }

public record NegationNode(SyntaxNode Node) : SyntaxNode { }

public record LogicalNegationNode(SyntaxNode Node) : SyntaxNode { }

public record IdentifierNode(String Text) : SyntaxNode { }

public record PrintNode(SyntaxNode Node) : SyntaxNode { }

public record ReturnNode(SyntaxNode Node) : SyntaxNode { }

public record DeclarationNode(SyntaxNode Left, SyntaxNode? Middle, SyntaxNode Right) : SyntaxNode { }

public enum BinaryOperator
{
	Colon,

	Plus,
	Minus,

	Multiply,
	Divide,

	EqualTo,
	NotEqualTo,

	LessThan,
	MoreThan,
	MoreThanOrEqualTo,
	LessThanOrEqualTo,

	MemberAccess
}

public static class BinaryOperatorExtensions
{
	public static int ToPrecendence(this BinaryOperator op) => op switch
	{
		BinaryOperator.Colon => 0,

		BinaryOperator.Plus or
		BinaryOperator.Minus => 1,

		BinaryOperator.Multiply or
		BinaryOperator.Divide => 2,

		BinaryOperator.EqualTo or
		BinaryOperator.NotEqualTo => 3,

		BinaryOperator.LessThan or
		BinaryOperator.MoreThan or
		BinaryOperator.MoreThanOrEqualTo or
		BinaryOperator.LessThanOrEqualTo => 4,

		BinaryOperator.MemberAccess => 5,

		_ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
	};
}

public record BinaryOperatorNode(SyntaxNode Left, BinaryOperator Operator, SyntaxNode Right) : SyntaxNode { }

public record IfNode(SyntaxNode Condition, Block Block, Block? ElseBlock = null) : SyntaxNode { }

public record Block(List<SyntaxNode> Statements) : SyntaxNode { }

public record CallNode(SyntaxNode Target, List<SyntaxNode> Arguments) : SyntaxNode { }

public record ParensNode(SyntaxNode? Node = null) : SyntaxNode { }

public record CommaNode(List<SyntaxNode> Items) : SyntaxNode { }

public record BracesNode(List<SyntaxNode> Items) : SyntaxNode { }

public record FunctionNode(List<SyntaxNode> Parameters, SyntaxNode? ReturnType, SyntaxNode Body) : SyntaxNode { }
