namespace EscLang.Lex;

public static class Lexer
{
	/// <summary>
	/// Lexes a file
	/// </summary>
	/// <param name="input">code</param>
	/// <returns>Sequence of <see cref="Lexeme" /></returns>
	public static List<Lexeme> GetLexemes(String input)
	{
		var lexemes = new List<Lexeme>();

		if (input.Length == 0)
		{
			return lexemes;
		}

		Int32 line = 1;
		Int32 column = 1;
		Int32 position = 0;
		Int32 previous = -1;
		while (position < input.Length)
		{
			// Assert that the lexer advanced to the next token
			if (previous == position) { throw SyntaxError("Lexer did not advance input", line, column); }
			previous = position;

			switch (input[position])
			{
				case '\n':
				{
					lexemes.Add(new(LexemeType.EndOfLine, "\n", position, line, column));
					position += 1;
					line += 1;
					column = 1;
					break;
				}
				case '\r':
				{
					var next = position + 1;
					if (next == input.Length) { throw SyntaxError("CR without LF at end of file", line, column); }
					if (input[next] != '\n') { throw SyntaxError("CR without LF", line, column); }
					lexemes.Add(new(LexemeType.EndOfLine, "\r\n", position, line, column));
					position += 2;
					line += 1;
					column = 1;
					break;
				}
				case ':':
				case ';':
				case '-':
				case '+':
				case '=':
				case '.':
				case '!':
				case ',':
				case '<':
				case '>':
				case '(':
				case ')':
				case '[':
				case ']':
				case '{':
				case '}':
				case '*':
				case '^':
				{
					var type = input[position].GetTypeOfSymbol();
					lexemes.Add(new(type, input[position].ToString(), position, line, column));
					position += 1;
					column += 1;
					break;
				}
				case '|':
				{
					var next = position + 1;
					if (next == input.Length) { throw SyntaxError("Invalid logical-or syntax", line, column); }

					if (input[next] == '|') { next++; }
					else { throw SyntaxError("Bitwise operators not supported", line, column); }
					var wordLength = next - position;
					var word = input.AsSpan(position, wordLength).ToString();
					lexemes.Add(new(LexemeType.LogicalOr, word, position, line, column));
					position = next;
					column += wordLength;
					break;
				}
				case '&':
				{
					var next = position + 1;
					if (next == input.Length) { throw SyntaxError("Invalid logical-and syntax", line, column); }

					if (input[next] == '&') { next++; }
					else { throw SyntaxError("Bitwise operators not supported", line, column); }
					var wordLength = next - position;
					var word = input.AsSpan(position, wordLength).ToString();
					lexemes.Add(new(LexemeType.LogicalAnd, word, position, line, column));
					position = next;
					column += wordLength;
					break;
				}
				case ' ':
				case '\t':
				{
					var next = position + 1;
					while (next < input.Length)
					{
						var next_char = input[next];
						if (next_char == ' ' || next_char == '\t')
						{
							next++;
							continue;
						}
						break;
					}
					var wordLength = next - position;
					var word = input.AsSpan(position, wordLength).ToString();
					lexemes.Add(new(LexemeType.Spaces, word, position, line, column));
					position = next;
					column += wordLength;
					break;
				}
				case '/':
				{
					var next = position + 1;
					if (next == input.Length) { throw SyntaxError("Invalid comment syntax", line, column); }
					if (input[next] == '/')
					{
						next += 1;
						while (next < input.Length)
						{
							var next_char = input[next];
							if (next_char == '\r' || next_char == '\n') { break; }
							next++;
						}
						var wordLength = next - position;
						var word = input.AsSpan(position, wordLength).ToString();
						lexemes.Add(new(LexemeType.Comment, word, position, line, column));
						position = next;
						column += wordLength;
						break;
					}
					else if (input[next] == '*')
					{
						throw SyntaxError("Block comments not yet implemented", line, column);
					}
					else
					{
						var type = LexemeType.Slash;
						lexemes.Add(new(type, input[position].ToString(), position, line, column));
						position += 1;
						column += 1;
						break;
					}
				}
				case '\'':
				{
					var next = position + 1;
					{
						if (next == input.Length) { throw SyntaxError("Invalid single quote syntax", line, column); }
						var next_char = input[next];
						if (next_char == '\\')
						{
							next++;
							if (next == input.Length) { throw SyntaxError("Invalid single quote syntax", line, column); }
						}
						next++;
						next_char = input[next];
						if (next_char != '\'') { throw SyntaxError("Invalid single quote syntax", line, column); }
					}

					var word = input.AsSpan(position, next - position + 1).ToString();
					lexemes.Add(new(LexemeType.LiteralChar, word, position, line, column));
					position = next + 1;
					column += word.Length;
					break;
				}
				case '"':
				{
					var next = position + 1;
					while (true)
					{
						if (next == input.Length) { throw SyntaxError("Invalid double quote syntax", line, column); }
						var next_char = input[next];
						if (next_char == '"') { break; }
						if (next_char == '\\')
						{
							next++;
							if (next == input.Length) { throw SyntaxError("Invalid double quote syntax", line, column); }
						}
						next++;
					}

					var word = input.AsSpan(position, next - position + 1).ToString();
					lexemes.Add(new(LexemeType.LiteralString, word, position, line, column));
					position = next + 1;
					column += word.Length;
					break;
				}
				case Char ch when Char.IsLetter(ch):
				case '_':
				{
					var next = position + 1;
					while (next < input.Length)
					{
						var next_char = input[next];
						if (Char.IsLetter(next_char) || Char.IsDigit(next_char) || next_char == '_')
						{
							next++;
							continue;
						}
						break;
					}
					var wordLength = next - position;
					var word = input.AsSpan(position, wordLength).ToString();
					lexemes.Add(new(LexemeType.Identifier, word, position, line, column));
					position = next;
					column += wordLength;
					break;
				}
				case Char ch when Char.IsDigit(ch):
				{
					var next = position + 1;
					while (next < input.Length)
					{
						var next_char = input[next];
						if (Char.IsDigit(next_char))
						{
							next++;
							continue;
						}
						break;
					}
					var wordLength = next - position;
					var word = input.AsSpan(position, wordLength).ToString();
					lexemes.Add(new(LexemeType.Number, word, position, line, column));
					position = next;
					column += wordLength;
					break;
				}
				default:
				{
					throw SyntaxError("Unexpected start of lexeme: " + input[position], line, column);
				}
			}
		}

		lexemes.Add(new Lexeme(LexemeType.EndOfFile, String.Empty, position, line, column));
		return lexemes;
	}

	private static String UnescapeString(String word)
	{
		var next = 0;
		while (word.IndexOf('\\', next) is var index && index != -1)
		{
			next = index + 1;
			switch (word[next])
			{
				case 'r':
				case 'n':
				case 't':
				case '"':
				case '\'':
				case '\\':
				{
					next++;
					break;
				}
				default:
				{
					throw new InvalidOperationException("Unescaped sequence");
				}
			}
		}

		word = word.Replace("\\\\", "\\");
		word = word.Replace("\\r", "\r");
		word = word.Replace("\\n", "\n");
		word = word.Replace("\\t", "\t");
		word = word.Replace("\\'", "'");
		word = word.Replace("\\\"", "\"");

		return word;
	}

	private static Exception SyntaxError(String message, Int32 line, Int32 column) => new InvalidOperationException("Syntax error at (" + line + ", " + column + "): " + message);

	public static String UnwrapString(Lexeme stringLexeme)
	{
		if (stringLexeme.Type != LexemeType.LiteralString && stringLexeme.Type != LexemeType.LiteralChar)
		{
			throw new InvalidOperationException("Cannot unwrap string for non literal strings");
		}
		var word = stringLexeme.Text[1..^1];
		return UnescapeString(word);
	}
}
