using EscLang.Parse;

namespace EscLang.Eval;

public static class Evaluator
{
	// HACK: convenience for C# switch expressions that do not have an appropriate type
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

		_ = EvaluateSyntaxNode(functionNode.Body, functionScope, environment);

		return nil;
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
		var stringValue = EvaluateString(node.Node, scope, environment);
		environment.ProgramOutput.WriteLine(stringValue);
		return nil;
	}

	private static String EvaluateString(SyntaxNode node, Scope scope, Environment environment)
	{
		return node switch
		{
			LiteralStringNode expression => expression.Text,

			IdentifierNode expression =>
				scope.Get(expression.Text) is SyntaxNode expressionSyntaxNode ?
					EvaluateString(expressionSyntaxNode, scope, environment) :
					throw new NotImplementedException($"{nameof(EvaluateString)}(IdentifierNode) did not find identifier in scope: {expression.Text}"),

			_ => throw new NotImplementedException($"{nameof(EvaluateString)} not implemented for node type: {node.GetType().Name}"),
		};
	}
}
