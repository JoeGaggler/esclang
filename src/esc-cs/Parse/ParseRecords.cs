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

public record DeclarationNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public enum BinaryOperator
{
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

public record ParensNode(List<SyntaxNode> Items) : SyntaxNode { }

// TODO: internal record only used as a precursor to CommaNode, should be obviated
public record CommaTempNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record CommaNode(List<SyntaxNode> Items) : SyntaxNode { }

public record BracesNode(List<SyntaxNode> Items) : SyntaxNode { }

public record FunctionNode(SyntaxNode Parameters, SyntaxNode? ReturnType, SyntaxNode Body) : SyntaxNode { }
