using static System.Console;

namespace EscLang;

class Program
{
	static int Main(string[] args)
	{
		var measurements = new List<(String, String)>();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		if (args.Length <= 0 || args[0] is not String filePath)
		{
			WriteLine("Must provide source file as the parameter.");
			return 1;
		}

		if (args.Length <= 1 || args[1] is not String outputPath)
		{
			outputPath = Environment.CurrentDirectory;
		}
		WriteLine(outputPath);

		String escSourceCode;
		try
		{
			escSourceCode = File.ReadAllText(filePath);
		}
		catch
		{
			WriteLine($"Unable to read source from path: {filePath}");
			return 1;
		}
		Measure("Read", measurements, stopwatch);

		// Lexer
		Span<Lex.Lexeme> lexemes;
		try
		{
			lexemes = Lex.Lexer.GetLexemes(escSourceCode).ToArray().AsSpan();
		}
		catch
		{
			WriteLine($"Unable to lex path: {filePath}");
			return 1;
		}
		Measure("Lex", measurements, stopwatch);

		// Parser
		if (!Parse.Parser.TryParse(lexemes, out var file, out var error))
		{
			var errorMessage = Printer.PrintParseError(lexemes, error);
			WriteLine(errorMessage);
			return 1;
		}
		Measure("Parse", measurements, stopwatch);

		// Evaluator
		var programOutput = new StringWriter();
		Eval.Evaluator.Evaluate(file, programOutput);
		Measure("Eval", measurements, stopwatch);

		// Print
		var programOutputString = programOutput.GetStringBuilder().ToString();
		Console.Write(programOutputString);
		Measure("Print", measurements, stopwatch);

		// Compiler not yet viable, so currently saving debug information as the output
		var outputFilePath = Path.Combine(outputPath, "output.txt");
		try
		{
			using (var outputFile = new StreamWriter(outputFilePath))
			{
				outputFile.WriteLine("Stats:");
				Stats(outputFile, measurements);

				// Debug Lexer
				outputFile.WriteLine();
				outputFile.WriteLine("Lex:");
				foreach (var lexeme in lexemes)
				{
					Printer.PrintLexeme(outputFile, lexeme);
				}

				// Debug Parser
				outputFile.WriteLine();
				outputFile.WriteLine("Parse:");
				Printer.PrintSyntax(outputFile, file, lexemes);

				// Debug Output
				outputFile.WriteLine();
				outputFile.WriteLine("Output:");
				outputFile.Write(programOutputString);
			}
		}
		catch
		{
			WriteLine($"Unable to write output to path: {outputPath}");
			return 1;
		}

		Stats(Console.Out, measurements);
		return 0;
	}

	private static void Measure(String name, List<(String, String)> measurements, System.Diagnostics.Stopwatch stopwatch)
	{
		var time = stopwatch.Elapsed;
		var nameString = name + ":";
		var timeString = time.ToString("s\\.ff");
		measurements.Add((nameString, timeString));
		stopwatch.Restart();
	}

	private static void Stats(TextWriter textWriter, List<(String, String)> measurements)
	{
		var width1 = measurements.Select(i => i.Item1.Length).Max();
		var width2 = measurements.Select(i => i.Item2.Length).Max();
		foreach (var item in measurements)
		{
			textWriter.WriteLine($"- {item.Item1.PadLeft(width1)} {item.Item2.PadLeft(width2)}");
		}
	}
}
