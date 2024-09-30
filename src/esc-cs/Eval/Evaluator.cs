using EscLang.Parse;

namespace EscLang.Eval;

// TODO: on crash, dump the AST with a pointer to the node that crashed

public static class Evaluator
{
	// HACK: special nodes only needed for the evaluator that signals to stop evaluation in the current scope
	public record ReturningNodeNode(SyntaxNode Node) : SyntaxNode { }
	public record ReturningVoidNode() : SyntaxNode { }
	public record ImplicitVoidNode() : SyntaxNode { }

	public static SyntaxNode Evaluate(EscFile file, StringWriter programOutput)
	{
		var environment = new Environment(programOutput);
		var globalScope = new Scope();

		foreach (var node in file.Lines)
		{
			switch (EvaluateSyntaxNode(node, globalScope, environment))
			{
				case ReturningNodeNode returningNodeNode:
				{
					return returningNodeNode.Node;
				}
				case ReturningVoidNode _:
				{
					break;
				}
			}
		}

		return new ReturningVoidNode();
	}

	public static SyntaxNode EvaluateSyntaxNode(SyntaxNode syntaxNode, Scope scope, Environment environment)
	{
		return syntaxNode switch
		{
			DeclareStaticNode node => EvaluateNode(node, scope, environment),
			CallNode node => EvaluateNode(node, scope, environment),
			Block node => EvaluateNode(node, scope, environment), // TODO: merge with BracesNode?
			BracesNode node => EvaluateNode(node, scope, environment),
			PrintNode node => EvaluateNode(node, scope, environment),
			ReturnNode node => EvaluateNode(node, scope, environment),
			IfNode node => EvaluateNode(node, scope, environment),
			IdentifierNode node => EvaluateNode(node, scope, environment),
			LogicalNegationNode node => EvaluateNode(node, scope, environment),
			BinaryOperatorNode node => EvaluateNode(node, scope, environment),

			// Literals
			LiteralCharNode node => node,
			LiteralNumberNode node => node,
			LiteralStringNode node => node,
			FunctionNode node => node,

			_ => throw new NotImplementedException($"{nameof(EvaluateSyntaxNode)} not implemented for node type: {syntaxNode.GetType().Name}"),
		};
	}

	private static SyntaxNode EvaluateNode(IdentifierNode node, Scope scope, Environment environment)
	{
		var identifier = node.Text;
		if (scope.Get(identifier) is not SyntaxNode expression)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(IdentifierNode) cannot find identifier in scope: {identifier}");
		}
		return expression;
	}

	private static SyntaxNode EvaluateNode(DeclareStaticNode node, Scope scope, Environment environment)
	{
		var left = node.Identifier;
		var right = node.Value;

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
		if (scope.Get(identifier) is not FunctionNode functionNode)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) not implemented for target object type: {scope.Get(identifier)?.GetType().Name}");
		}
		if (functionNode.Parameters is not List<SyntaxNode> actualParameterList)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) not implemented for parameters node type: {functionNode.Parameters.GetType().Name}");
		}
		if (node.Arguments.Count != actualParameterList.Count)
		{
			throw new NotImplementedException($"{nameof(EvaluateNode)}(CallNode) argument count mismatch: {node.Arguments.Count} != {actualParameterList.Count} {actualParameterList[0]}");
		}

		var functionScope = new Scope(scope);

		for (int i = 0; i < node.Arguments.Count; i++)
		{
			if (actualParameterList[i] is not DeclareStaticNode parameterDeclarationNode)
			{
				throw new NotImplementedException($"Invalid parameter for CallNode: {actualParameterList[i]}");
			}
			if (parameterDeclarationNode.Identifier is not IdentifierNode parameterIdentifierNode || parameterIdentifierNode.Text is not { } parameterIdentifier)
			{
				throw new NotImplementedException($"Invalid parameter name identifier for CallNode: {parameterDeclarationNode.Identifier}");
			}
			if (parameterDeclarationNode.Type is not IdentifierNode parameterTypeIdentifierNode || parameterTypeIdentifierNode.Text is not { } parameterTypeIdentifier)
			{
				throw new NotImplementedException($"Invalid parameter type identifier for CallNode: {parameterDeclarationNode.Type}");
			}

			var arg = node.Arguments[i];
			TypeCheck(expectedTypeName: parameterTypeIdentifier, actualExpression: arg, scope, environment);
			functionScope.Store[parameterIdentifier] = arg;
		}

		// CallNode should be the only node that "unwraps" the Returning*Node values
		var returnValue = EvaluateSyntaxNode(functionNode.Body, functionScope, environment);
		switch (returnValue)
		{
			case ReturningNodeNode returningNode:
			{
				// TODO: bring back type-checking with new function syntax
				//
				// if (declObj.Middle is FunctionDeclarationNode { ReturnType: { } returnTypeNode })
				// {
				// 	if (returnTypeNode is not IdentifierNode returnTypeIdentifierNode || returnTypeIdentifierNode.Text is not { } returnTypeIdentifier)
				// 	{
				// 		throw new NotImplementedException($"Invalid return type for CallNode: {returnTypeNode}");
				// 	}

				// 	TypeCheck(expectedTypeName: returnTypeIdentifier, actualExpression: returningNode.Node, scope, environment);
				// }
				// if (functionDeclarationNode.ReturnType is not { } returnTypeNode)
				// {
				// 	throw new InvalidOperationException($"Function returned {returningNode.Node}, but declared void return type");
				// }
				// if (returnTypeNode is not IdentifierNode returnTypeIdentifierNode || returnTypeIdentifierNode.Text is not { } returnTypeIdentifier)
				// {
				// 	throw new NotImplementedException($"Invalid return type for CallNode: {returnTypeNode}");
				// }

				// TypeCheck(expectedTypeName: returnTypeIdentifier, actualExpression: returningNode.Node, scope, environment);

				return returningNode.Node;
			}
			case SyntaxNode voidNode when voidNode is ReturningNodeNode or ImplicitVoidNode:
			{
				if (functionNode.ReturnType is not null)
				{
					throw new InvalidOperationException($"Function returned void, but declared return type {functionNode.ReturnType}");
				}
				return voidNode;
			}
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
			case "int":
			{
				switch (actualExpression)
				{
					case LiteralNumberNode _: return;
				}
				break;
			}
		}

		throw new NotImplementedException($"{nameof(TypeCheck)} failed: left={expectedTypeName}, right={actualExpression}");
	}

	private static SyntaxNode EvaluateNode(BracesNode node, Scope scope, Environment environment)
	{
		var bodyScope = new Scope(scope);

		foreach (var childNode in node.Lines)
		{
			var result = EvaluateSyntaxNode(childNode, bodyScope, environment);

			// If the child node is a return node, return the value
			if (result is ReturningNodeNode || result is ReturningVoidNode)
			{
				return result;
			}
		}

		return new ImplicitVoidNode();
	}

	// TODO: merge with BracesNode?
	private static SyntaxNode EvaluateNode(Block node, Scope scope, Environment environment)
	{
		var bodyScope = new Scope(scope);

		foreach (var childNode in node.Statements)
		{
			var result = EvaluateSyntaxNode(childNode, bodyScope, environment);

			// If the child node is a return node, return the value
			if (result is ReturningNodeNode || result is ReturningVoidNode)
			{
				return result;
			}
		}

		return new ImplicitVoidNode();
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

	private static SyntaxNode EvaluateNode(IfNode node, Scope scope, Environment environment)
	{
		var condition = EvaluateSyntaxNode(node.Condition, scope, environment);
		var test = condition switch
		{
			LiteralNumberNode literalNumberNode => literalNumberNode.Text switch
			{
				"0" => false,
				"1" => true,
				_ => throw new NotImplementedException($"Invalid condition for IfNode: {condition}")
			},
			_ => throw new NotImplementedException($"Invalid condition for IfNode: {condition}")
		};

		if (test)
		{
			return EvaluateSyntaxNode(node.Block, scope, environment);
		}
		else if (node.ElseBlock is { } elseBlock)
		{
			return EvaluateSyntaxNode(elseBlock, scope, environment);
		}
		else
		{
			return new ImplicitVoidNode(); // block not executed
		}
	}

	private static SyntaxNode EvaluateNode(LogicalNegationNode node, Scope scope, Environment environment)
	{
		var condition = EvaluateSyntaxNode(node.Node, scope, environment);
		return condition switch
		{
			LiteralNumberNode literalNumberNode => literalNumberNode.Text switch
			{
				"0" => new LiteralNumberNode("1"),
				_ => new LiteralNumberNode("0"),
			},
			_ => throw new NotImplementedException($"Invalid condition for LogicalNegationNode: {condition}")
		};
	}

	private static SyntaxNode EvaluateNode(BinaryOperatorNode node, Scope scope, Environment environment)
	{
		var left = EvaluateSyntaxNode(node.Left, scope, environment);
		var right = EvaluateSyntaxNode(node.Right, scope, environment);

		return (left, right) switch
		{
			(LiteralNumberNode leftLiteralNumberNode, LiteralNumberNode rightLiteralNumberNode) => node.Operator switch
			{
				BinaryOperator.Plus => new LiteralNumberNode((Int32.Parse(leftLiteralNumberNode.Text) + Int32.Parse(rightLiteralNumberNode.Text)).ToString()),
				BinaryOperator.Minus => new LiteralNumberNode((Int32.Parse(leftLiteralNumberNode.Text) - Int32.Parse(rightLiteralNumberNode.Text)).ToString()),
				BinaryOperator.Multiply => new LiteralNumberNode((Int32.Parse(leftLiteralNumberNode.Text) * Int32.Parse(rightLiteralNumberNode.Text)).ToString()),
				BinaryOperator.Divide => new LiteralNumberNode((Int32.Parse(leftLiteralNumberNode.Text) / Int32.Parse(rightLiteralNumberNode.Text)).ToString()),
				_ => throw new NotImplementedException($"Invalid operator for BinaryOperatorNode: {node.Operator}")
			},
			_ => throw new NotImplementedException($"Invalid operands for BinaryOperatorNode: {left}, {right}")
		};
	}

	private static String EvaluateString(SyntaxNode node, Scope scope, Environment environment)
	{
		return node switch
		{
			LiteralCharNode expression => expression.Text,
			LiteralStringNode expression => expression.Text,
			LiteralNumberNode expression => expression.Text,
			BinaryOperatorNode expression => EvaluateString(EvaluateNode(expression, scope, environment), scope, environment),

			IdentifierNode expression =>
				scope.Get(expression.Text) is SyntaxNode expressionSyntaxNode ?
					EvaluateString(expressionSyntaxNode, scope, environment) :
					throw new NotImplementedException($"{nameof(EvaluateString)}(IdentifierNode) did not find identifier in scope: {expression.Text}"),

			_ => throw new NotImplementedException($"{nameof(EvaluateString)} not implemented for node type: {node.GetType().Name}"),
		};
	}
}
