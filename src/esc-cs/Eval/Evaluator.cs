using EscLang.Parse;

namespace EscLang.Eval;

// TODO: on crash, dump the AST with a pointer to the node that crashed

public static class Evaluator
{
	// HACK: special nodes only needed for the evaluator that signals to stop evaluation in the current scope
	public record ReturningNodeNode(SyntaxNode Node) : SyntaxNode { }
	public record ReturningVoidNode() : SyntaxNode { }

	public static void Evaluate(EscFile file, StringWriter programOutput)
	{
		var environment = new Environment(programOutput);
		var globalScope = new Scope();

		foreach (var node in file.Nodes)
		{
			EvaluateSyntaxNode(node, globalScope, environment);
		}
	}

	private static SyntaxNode EvaluateSyntaxNode(SyntaxNode syntaxNode, Scope scope, Environment environment)
	{
		return syntaxNode switch
		{
			DeclarationNode node => EvaluateNode(node, scope, environment),
			CallNode node => EvaluateNode(node, scope, environment),
			BracesNode node => EvaluateNode(node, scope, environment),
			PrintNode node => EvaluateNode(node, scope, environment),
			ReturnNode node => EvaluateNode(node, scope, environment),

			// Literals
			LiteralCharNode node => node,
			LiteralNumberNode node => node,
			LiteralStringNode node => node,
			FunctionNode node => node,

			_ => throw new NotImplementedException($"{nameof(EvaluateSyntaxNode)} not implemented for node type: {syntaxNode.GetType().Name}"),
		};
	}

	private static SyntaxNode EvaluateNode(DeclarationNode node, Scope scope, Environment environment)
	{
		var left = node.Left;
		var right = node.Right;

		if (left is not IdentifierNode identifierNode)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)} not implemented for left node type: {left.GetType().Name}");
		}

		var identifier = identifierNode.Text;
		var expression = EvaluateSyntaxNode(right, scope, environment);
		scope.Store[identifier] = expression;

		return left; // the "l-value"
	}

	private static SyntaxNode EvaluateNode(CallNode node, Scope scope, Environment environment)
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

		for (int i = 0; i < node.Arguments.Count; i++)
		{
			if (parensNode.Items[i] is not DeclarationNode parameterDeclarationNode)
			{
				throw new NotImplementedException($"Invalid parameter for CallNode: {parensNode.Items[i]}");
			}
			if (parameterDeclarationNode.Left is not IdentifierNode parameterIdentifierNode || parameterIdentifierNode.Text is not { } parameterIdentifier)
			{
				throw new NotImplementedException($"Invalid parameter name identifier for CallNode: {parameterDeclarationNode.Left}");
			}
			if (parameterDeclarationNode.Right is not IdentifierNode parameterTypeIdentifierNode || parameterTypeIdentifierNode.Text is not { } parameterTypeIdentifier)
			{
				throw new NotImplementedException($"Invalid parameter type identifier for CallNode: {parameterDeclarationNode.Right}");
			}

			var arg = node.Arguments[i];
			TypeCheck(expectedTypeName: parameterTypeIdentifier, actualExpression: arg, scope, environment);
			functionScope.Store[parameterIdentifier] = arg;
		}

		// CallNode should be the only node that "unwraps" the Returning*Node values
		var returnValue = EvaluateSyntaxNode(functionNode.Body, functionScope, environment);
		switch (returnValue)
		{
			case ReturningNodeNode returningNode: return returningNode.Node;
			case ReturningVoidNode returningNode: return returningNode;
			default:
			{
				throw new NotImplementedException($"Invalid return value for CallNode: {returnValue}");
			}
		}
	}

	private static void TypeCheck(String expectedTypeName, SyntaxNode actualExpression, Scope scope, Environment environment)
	{
		switch (expectedTypeName)
		{
			case "String":
			{
				switch (actualExpression)
				{
					case LiteralStringNode _: return;
					case IdentifierNode identifierNode when scope.Get(identifierNode.Text) is SyntaxNode identifierSyntaxNode:
					{
						TypeCheck(expectedTypeName, identifierSyntaxNode, scope, environment);
						return;
					}
				}
				break;
			}
		}

		throw new NotImplementedException($"{nameof(TypeCheck)} failed: left={expectedTypeName}, right={actualExpression}");
	}

	private static SyntaxNode EvaluateNode(BracesNode node, Scope scope, Environment environment)
	{
		var bodyScope = new Scope(scope);

		foreach (var childNode in node.Items)
		{
			var result = EvaluateSyntaxNode(childNode, bodyScope, environment);

			// If the child node is a return node, return the value
			if (result is ReturningNodeNode || result is ReturningVoidNode)
			{
				return result;
			}
		}

		return new ReturningVoidNode();
	}

	private static SyntaxNode EvaluateNode(PrintNode node, Scope scope, Environment environment)
	{
		var expression = node.Node;
		var stringValue = EvaluateString(expression, scope, environment);
		environment.ProgramOutput.WriteLine(stringValue);
		return expression; // passthrough
	}

	private static SyntaxNode EvaluateNode(ReturnNode node, Scope scope, Environment environment)
	{
		var evaluated = EvaluateSyntaxNode(node.Node, scope, environment);
		return new ReturningNodeNode(evaluated);
	}

	private static String EvaluateString(SyntaxNode node, Scope scope, Environment environment)
	{
		return node switch
		{
			LiteralCharNode expression => expression.Text,
			LiteralStringNode expression => expression.Text,
			LiteralNumberNode expression => expression.Text,

			IdentifierNode expression =>
				scope.Get(expression.Text) is SyntaxNode expressionSyntaxNode ?
					EvaluateString(expressionSyntaxNode, scope, environment) :
					throw new NotImplementedException($"{nameof(EvaluateString)}(IdentifierNode) did not find identifier in scope: {expression.Text}"),

			_ => throw new NotImplementedException($"{nameof(EvaluateString)} not implemented for node type: {node.GetType().Name}"),
		};
	}
}
