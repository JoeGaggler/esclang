namespace EscLang.Parse;

public abstract record SyntaxNode { }

public record EscFile(List<SyntaxNode> Lines) : SyntaxNode { }

public record BracesNode(List<SyntaxNode> Lines) : SyntaxNode { }

public record DotNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record CallNode(SyntaxNode Target, List<SyntaxNode> Arguments) : SyntaxNode { }

public record DeclareNode(SyntaxNode Identifier, SyntaxNode? Type) : SyntaxNode { }

public record DeclareStaticNode(SyntaxNode Identifier, SyntaxNode? Type, SyntaxNode? Value) : SyntaxNode { }

public record DeclareAssignNode(SyntaxNode Identifier, SyntaxNode? Type, SyntaxNode? Value) : SyntaxNode { }

public record MemberNode(SyntaxNode Target, SyntaxNode Member) : SyntaxNode { }

public record AssignNode(SyntaxNode Target, SyntaxNode Value) : SyntaxNode { }

public record PlusNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record MinusNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record StarNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record SlashNode(SyntaxNode Left, SyntaxNode Right) : SyntaxNode { }

public record ParameterNode : SyntaxNode { }

public record LeftArrowNode : SyntaxNode { }

public record EmptyNode : SyntaxNode { }


public record LiteralCharNode(String Text) : SyntaxNode { }

public record LiteralNumberNode(String Text) : SyntaxNode { }

public record LiteralStringNode(String Text) : SyntaxNode { }

public record NegationNode(SyntaxNode Node) : SyntaxNode { }

public record LogicalNegationNode(SyntaxNode Node) : SyntaxNode { }

public record IdentifierNode(String Text) : SyntaxNode { }

public record ParensNode(SyntaxNode? Node = null) : SyntaxNode { }
