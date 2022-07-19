using EscLang.Lex;

namespace EscLang.Parse;

partial class Parser
{
	/// <summary>
	/// Parses a code block
	/// </summary>
	/// <remarks>
	/// Preconditions: after the open-brace
	/// Postcondition: after the close-brace
	/// </remarks>
	/// <param name="input">input</param>
	/// <param name="start">start</param>
	/// <returns><see cref="SyntaxNode"/> result</returns>
	private static ParseResult<Block> Parse_Block(ReadOnlySpan<Lexeme> input, ref int start)
	{
		var position = start;
		var nodes = new List<SyntaxNode>();
		while (true)
		{
			var (peek, next) = input.Peek(position);
			switch (peek.Type)
			{
				case LexemeType.EndOfFile:
				{
					return new(input[start], Error.Message("end of file before closing brace"));
				}
				case LexemeType.BraceClose:
				{
					start = next;
					var file = new Block(nodes);
					return new(file);
				}
				case LexemeType.EndOfLine:
				{
					position = next;
					break;
				}
				case LexemeType.Identifier when peek.Text == "print":
				{
					position = next;
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue)
					{
						return new(input[start], Error.Message("invalid print expression"), node.Error);
					}

					nodes.Add(new PrintNode(node.Value));
					break;
				}
				case LexemeType.Identifier when peek.Text == "if":
				{
					var node = Parse_If(input, ref next);
					if (!node) { return new(input[position], Error.Message("invalid print expression"), node.Error); }
					position = next;
					nodes.Add(new PrintNode(node.Value));
					break;
				}
				default:
				{
					var node = Parse_File_Expression(input, ref position);
					if (!node.HasValue)
					{
						return new(peek, Error.Message("failed top level statement"), node.Error);
					}

					nodes.Add(node.Value);
					break;
				}
			}
		}
	}
}