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
	Comma,

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

public record BinaryOperatorNode(SyntaxNode Left, BinaryOperator Operator, SyntaxNode Right) : SyntaxNode { }

public record IfNode(SyntaxNode Condition, Block Block, Block? ElseBlock = null) : SyntaxNode { }

public record Block(List<SyntaxNode> Statements) : SyntaxNode { }

public record CallNode(SyntaxNode Target, List<SyntaxNode> Arguments) : SyntaxNode { }

public record ParensNode(SyntaxNode? Node = null) : SyntaxNode { }

public record CommaNode(List<SyntaxNode> Items) : SyntaxNode { }

public record BracesNode(List<SyntaxNode> Items) : SyntaxNode { }

// TODO: return types are no longer embedded in the function node
public record FunctionNode(List<SyntaxNode> Parameters, [property:Obsolete] SyntaxNode? ReturnType, SyntaxNode Body) : SyntaxNode { }

public record FunctionDeclarationNode(SyntaxNode? ReturnType) : SyntaxNode { }
