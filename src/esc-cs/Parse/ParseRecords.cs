namespace EscLang.Parse;

public record EscFile(List<SyntaxNode> Nodes) { }

public abstract record SyntaxNode { }

public record LiteralCharNode(String Text) : SyntaxNode { }

public record LiteralNumberNode(String Text) : SyntaxNode { }

public record LiteralStringNode(String Text) : SyntaxNode { }

public record NegationNode(SyntaxNode Node) : SyntaxNode { }

public record IdentifierNode(String Text) : SyntaxNode { }

public record PrintNode(SyntaxNode Node) : SyntaxNode { }
