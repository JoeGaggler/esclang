using EscLang.Lex;

namespace EscLang.Parse;

partial class Parser
{
	private static class Error
	{
        public static String NotImplemented(String source) => $"{source}: not implemented yet";
		public static String UnexpectedToken(String source, Lexeme lexeme) => $"{source}: unexpected token: {lexeme.Type}";
	}
}
