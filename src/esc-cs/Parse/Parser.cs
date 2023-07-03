using System.Diagnostics.CodeAnalysis;

using EscLang.Lex;

namespace EscLang.Parse;

public static partial class Parser
{
	/// <summary>
	/// Tries to parses a file
	/// </summary>
	/// <remarks>
	/// Preconditions: start of a file
	/// Postcondition: end of a file
	/// </remarks>
	/// <param name="input">code</param>
	/// <param name="error">parse error, or <see cref="null"/> if parsing succeeded</param>
	/// <returns><see cref="File"/> result, or <see cref="null"/> if parsing failed</returns>
	public static Boolean TryParse(ReadOnlySpan<Lexeme> lexemes, [NotNullWhen(true)] out EscFile? file, [NotNullWhen(false)] out ParseError? error)
	{
		var start = 0;
		var parsedFile = Parse_File(lexemes, ref start);
		if (!parsedFile.HasValue)
		{
			error = parsedFile.Error!;
			file = null;
			return false;
		}

		error = null;
		file = parsedFile.Value;
		return true;
	}
}