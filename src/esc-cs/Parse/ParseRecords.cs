namespace EscLang.Parse;

public record EscFile(List<LineNode> Lines) { }

public abstract record SyntaxNode { }

public record LineNode(List<SyntaxNode> Items) : SyntaxNode { }

public record BracesNode(List<LineNode> Lines) : SyntaxNode { }

public record DotNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record CallNode(SyntaxNode Target, List<SyntaxNode> Arguments) : SyntaxNode { }

public record DeclareNode(SyntaxNode Identifier, SyntaxNode? Type) : SyntaxNode { }

public record DeclareStaticNode(SyntaxNode Identifier, SyntaxNode? Type, SyntaxNode? Value) : SyntaxNode { }

public record DeclareAssignNode(SyntaxNode Identifier, SyntaxNode? Type, SyntaxNode? Value) : SyntaxNode { }

public record AssignNode(SyntaxNode Assignee, SyntaxNode Value) : SyntaxNode { }

public record PlusNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record MinusNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record StarNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record SlashNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record ParameterNode : SyntaxNode { }

public record EmptyNode : SyntaxNode { }

//
// TODO: migrate older node types
//

public record LiteralCharNode(String Text) : SyntaxNode { }

public record LiteralNumberNode(String Text) : SyntaxNode { }

public record LiteralStringNode(String Text) : SyntaxNode { }

public record NegationNode(SyntaxNode Node) : SyntaxNode { }

public record LogicalNegationNode(SyntaxNode Node) : SyntaxNode { }

public record IdentifierNode(String Text) : SyntaxNode { }

public record PrintNode(SyntaxNode Node) : SyntaxNode { }

public record ReturnNode(SyntaxNode Node) : SyntaxNode { }


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

public record ParensNode(SyntaxNode? Node = null) : SyntaxNode { }

public record CommaNode(List<SyntaxNode> Items) : SyntaxNode { }


// TODO: return types are no longer embedded in the function node
public record FunctionNode(List<SyntaxNode> Parameters, [property:Obsolete] SyntaxNode? ReturnType, SyntaxNode Body) : SyntaxNode { }

public record FunctionDeclarationNode(SyntaxNode? ReturnType) : SyntaxNode { }
