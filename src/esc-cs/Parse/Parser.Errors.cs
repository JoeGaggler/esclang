using EscLang.Lex;

namespace EscLang.Parse;

partial class Parser
{
	private static class Error
	{
		public static String Message(String message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "UNKNOWN_CALLER") => $"{caller} → {message}";
		public static String NotImplemented(String message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "UNKNOWN_CALLER") => $"{caller} → not implemented yet: {message}";
		public static String UnexpectedToken(Lexeme lexeme, [System.Runtime.CompilerServices.CallerMemberName] string caller = "UNKNOWN_CALLER") => $"{caller} → unexpected token: {lexeme.Type}";
	}
}
