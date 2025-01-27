using static System.Console;

Run("cases/first.esc", NoResult());
Run("cases/first-print-return.esc", Text("hello, world"));
Run("cases/simple-reassignment.esc", Number(4));
Run("cases/blocks.esc", Number(16));
Run("cases/func-params.esc", Number(13));
Run("cases/func-params-2.esc", Number(25));
Run("cases/recursive.esc", Number(111));
Run("cases/two-func-calls.esc", Number(999));
Run("cases/two-func-calls-reverse-declaration.esc", Number(999)); // TODO: out of order declarations

static void Run(String testCasePath, EscLang.Eval.Evaluation expected)
{
    var escSourceCode = File.ReadAllText(testCasePath);
    var lexemes = EscLang.Lex.Lexer.GetLexemes(escSourceCode).ToArray().AsSpan();

    if (!EscLang.Parse.Parser.TryParse(lexemes, out var file, out var error))
    {
        var errorMessage = EscLang.Printer.PrintParseError(lexemes, error);
		WriteLine($"Failed: {testCasePath} --" + " Syntax - " + errorMessage);
		return;
	}

	EscLang.Analyze.Analysis analysis;
	try
	{
		analysis = EscLang.Analyze.Analyzer.Analyze(file, StreamWriter.Null);
	}
	catch (Exception e)
	{
		WriteLine($"Failed: {testCasePath} --" + " Analysis - " + e.ToString());
		return;
	}

    var programOutput = new StringWriter();
    EscLang.Eval.Evaluation actual;
    try
    {
        actual = EscLang.Eval.Evaluator.Evaluate(EscLang.Analyze.Table.Instance, programOutput);
    }
    catch (Exception e)
    {
        programOutput.WriteLine("*** CRASH! ***");
        programOutput.WriteLine(e.ToString());
        WriteLine($"Failed: Crash - " + e.ToString());
		WriteLine($"Failed: {testCasePath} --" + " Crash - " + e.ToString());
		return;
	}

    if (actual != expected)
    {
        WriteLine($"Failed: {testCasePath} --" + " Expected: " + expected + ", Actual: " + actual);
        return;
    }

    WriteLine("Passed: " + testCasePath);
}

static EscLang.Eval.Evaluation NoResult() => EscLang.Eval.VoidEvaluation.Instance;
static EscLang.Eval.Evaluation Number(int value) => new EscLang.Eval.IntEvaluation(value);
static EscLang.Eval.Evaluation Text(String value) => new EscLang.Eval.StringEvaluation(value);