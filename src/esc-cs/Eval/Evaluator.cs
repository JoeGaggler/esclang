using EscLang.Parse;

namespace EscLang.Eval;

public static class Evaluator
{
	private struct Nil { }
	private static readonly Nil nil = new();

	public static void Evaluate(EscFile file, StringWriter programOutput)
	{
		var environment = new Environment(programOutput);
		var globalScope = new Scope();

		foreach (var node in file.Nodes)
		{
			EvaluateSyntaxNode(node, globalScope, environment);
		}
	}

	private static Nil EvaluateSyntaxNode(SyntaxNode syntaxNode, Scope scope, Environment environment)
	{
		_ = syntaxNode switch
		{
			DeclarationNode node => EvaluateNode(node, scope, environment),
			CallNode node => EvaluateNode(node, scope, environment),
			BracesNode node => EvaluateNode(node, scope, environment),
			PrintNode node => EvaluateNode(node, scope, environment),
			_ => throw new NotImplementedException($"{nameof(EvaluateSyntaxNode)} not implemented for node type: {syntaxNode.GetType().Name}"),
		};

		return nil;
	}

	private static Nil EvaluateNode(DeclarationNode node, Scope scope, Environment environment)
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

	private static Nil EvaluateNode(CallNode node, Scope scope, Environment environment)
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

		_ = EvaluateSyntaxNode(functionNode.Body, functionScope, environment);

		return nil;
	}

	private static Nil EvaluateNode(BracesNode node, Scope scope, Environment environment)
	{
		var bodyScope = new Scope(scope);

		foreach (var childNode in node.Items)
		{
			EvaluateSyntaxNode(childNode, bodyScope, environment);
		}

		return nil;
	}

	private static Nil EvaluateNode(PrintNode node, Scope scope, Environment environment)
	{
		String stringValue = node.Node switch
		{
			LiteralStringNode expression => expression.Text,
			_ => throw new NotImplementedException($"{nameof(EvaluateNode)}(PrintNode) not implemented for node type: {node.GetType().Name}"),
		};

		environment.ProgramOutput.WriteLine(stringValue);

		return nil;
	}
}
