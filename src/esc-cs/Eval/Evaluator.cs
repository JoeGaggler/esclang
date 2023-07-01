using EscLang.Parse;

namespace EscLang.Eval;

public static class Evaluator
{
	private struct Nil { }
	private static readonly Nil nil = new();

	public static void Evaluate(EscFile file)
	{
		var globalScope = new Scope();

		foreach (var node in file.Nodes)
		{
			EvaluateSyntaxNode(node, globalScope);
		}
	}

	private static Nil EvaluateSyntaxNode(SyntaxNode syntaxNode, Scope scope)
	{
		_ = syntaxNode switch
		{
			DeclarationNode node => EvaluateNode(node, scope),
			CallNode node => EvaluateNode(node, scope),
			BracesNode node => EvaluateNode(node, scope),
			PrintNode node => EvaluateNode(node, scope),
			_ => throw new NotImplementedException($"{nameof(EvaluateSyntaxNode)} not implemented for node type: {syntaxNode.GetType().Name}"),
		};

		return nil;
	}

	private static Nil EvaluateNode(DeclarationNode node, Scope scope)
	{
		var left = node.Left;
		var right = node.Right;

		if (left is not IdentifierNode identifierNode)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)} not implemented for left node type: {left.GetType().Name}");
		}

		var identifier = identifierNode.Text;
		var expression = right;
		scope.Store[identifier] = expression;

		return nil;
	}

	private static Nil EvaluateNode(CallNode node, Scope scope)
	{
		if (node.Target is not IdentifierNode identifierNode || identifierNode.Text is not { } identifier)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) not implemented for target node type: {node.Target.GetType().Name}");
		}
		if (scope.Get(identifier) is not Object rightObject)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) cannot find identifier in scope: {identifier}");
		}
		if (rightObject is not FunctionNode functionNode)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) not implemented for target object type: {rightObject.GetType().Name}");
		}
		if (functionNode.Parameters is not ParensNode parensNode)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) not implemented for parameters node type: {functionNode.Parameters.GetType().Name}");
		}
		if (node.Arguments.Count != parensNode.Items.Count)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) argument count mismatch: {node.Arguments.Count} != {parensNode.Items.Count}");
		}

		var functionScope = new Scope(scope);

		_ = EvaluateSyntaxNode(functionNode.Body, functionScope);

		return nil;
	}

	private static Nil EvaluateNode(BracesNode node, Scope scope)
	{
		var bodyScope = new Scope(scope);

		foreach (var childNode in node.Items)
		{
			EvaluateSyntaxNode(childNode, bodyScope);
		}

		return nil;
	}

	private static Nil EvaluateNode(PrintNode node, Scope scope)
	{
		String stringValue = node.Node switch
		{
			LiteralStringNode expression => expression.Text,
			_ => throw new NotImplementedException($"{nameof(EvaluateNode)}(PrintNode) not implemented for node type: {node.GetType().Name}"),
		};

		Console.WriteLine(stringValue);

		return nil;
	}
}
