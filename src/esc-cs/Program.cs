using static System.Console;

namespace EscLang;

class Program
{
	static int Main(string[] args)
	{
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		var measurements = new List<(String, String)>();
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

		var outputFilePath = Path.Combine(outputPath, "output.txt");
		using var outputFile = new StreamWriter(outputFilePath);

		Measure("Start", measurements, stopwatch);

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

		// Debug Lexer
		outputFile.WriteLine();
		outputFile.WriteLine("Lex:");
		int lexemeIndex = 0;
		foreach (var lexeme in lexemes)
		{
			Printer.PrintLexeme(outputFile, lexeme, lexemeIndex);
			lexemeIndex++;
		}

		// Parser
		if (!Parse.Parser.TryParse(lexemes, out var file, out var error))
		{
			var errorMessage = Printer.PrintParseError(lexemes, error);
			WriteLine(errorMessage);
			return 1;
		}
		Measure("Parse", measurements, stopwatch);

		// Debug Parser
		outputFile.WriteLine();
		outputFile.WriteLine("Parse:");
		Printer.PrintSyntax(outputFile, file, lexemes);

		// Analyzer
		outputFile.WriteLine();
		outputFile.WriteLine("Analysis Log:");
		var unit = Analyze.Analyzer.Analyze(file, outputFile);
		Measure("Analyze", measurements, stopwatch);

		// Debug Analyzer
		outputFile.WriteLine();
		outputFile.WriteLine("Analyze Types:");
		Printer.PrintTypeTable2(unit, outputFile);

		// Evaluator
		var programOutput = new StringWriter();
		try
		{
			Eval.Evaluator.Evaluate(unit, programOutput);
		}
		catch (Exception e)
		{
			programOutput.WriteLine("*** CRASH! ***");
			programOutput.WriteLine(e.ToString());
		}
		Measure("Eval", measurements, stopwatch);


		// Print
		var programOutputString = programOutput.GetStringBuilder().ToString();
		Console.Write(programOutputString);
		Measure("Print", measurements, stopwatch);

		// Debug Output
		outputFile.WriteLine();
		outputFile.WriteLine("Output:");
		outputFile.Write(programOutputString);
		Measure("Output", measurements, stopwatch);

		// Stats
		outputFile.WriteLine();
		outputFile.WriteLine("Stats:");
		Stats(outputFile, measurements);
		return 0;
	}

	private static void Measure(String name, List<(String, String)> measurements, System.Diagnostics.Stopwatch stopwatch)
	{
		var time = stopwatch.Elapsed;
		var nameString = name + ":";
		var timeString = time.ToString("s\\.f\\s");
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
