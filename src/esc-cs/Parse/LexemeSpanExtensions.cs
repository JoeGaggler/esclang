using EscLang.Lex;

internal static class LexemeSpanExtensions
{
	public static (Lexeme, Int32) Peek(this ReadOnlySpan<Lexeme> input, Int32 start)
	{
		var position = start;
		while (true)
		{
			if (position >= input.Length)
			{
				throw new InvalidOperationException($"{nameof(Peek)}@{position}: expected to see EndOfFile token");
			}

			var next = input[position];
			var type = next.Type;
			switch (type)
			{
				case LexemeType.Spaces: break; // Read through spaces
				case LexemeType.Comment: break; // Read through comments
				case LexemeType.EndOfFile:
				{
					return (next, position); // Already at end, don't go farther
				}
				default:
				{
					return (next, position + 1);
				}
			}
			position++;
		}
	}

	public static Lexeme Consume(this ReadOnlySpan<Lexeme> input, ref Int32 start)
	{
		var (token, position) = input.Peek(start);
		start = position;
		return token;
	}

	public static Boolean ConsumeAll(this ReadOnlySpan<Lexeme> input, ref Int32 start, params LexemeType[] lexemeTypes)
	{
		var position = start;
		foreach (var t in lexemeTypes)
		{
			var (p1, p2) = Peek(input, position);
			if (p1.Type != t) { return false; }
			position = p2;
		}

		start = position;
		return true;
	}

	public static LexemeType? ConsumeAny(this ReadOnlySpan<Lexeme> input, ref Int32 start, params LexemeType[] lexemeTypes)
	{
		var (peek, next) = input.Peek(start);

		var type = peek.Type;
		foreach (var choice in lexemeTypes)
		{
			if (choice == type)
			{
				start = next;
				return type;
			}
		}
		return null;
	}
}
