using static System.Console;

WriteLine(Environment.CurrentDirectory);

Run("cases/first.esc", NoResult());
Run("cases/simple-reassignment.esc", Number(2));

static void Run(String testCasePath, EscLang.Parse.SyntaxNode expected)
{
    var escSourceCode = File.ReadAllText(testCasePath);
    var lexemes = EscLang.Lex.Lexer.GetLexemes(escSourceCode).ToArray().AsSpan();

    if (!EscLang.Parse.Parser.TryParse(lexemes, out var file, out var error))
    {
        var errorMessage = EscLang.Printer.PrintParseError(lexemes, error);
        WriteLine("Failed: Syntax - " + errorMessage);
        return;
    }

    var programOutput = new StringWriter();
    EscLang.Parse.SyntaxNode actual;
    try
    {
        actual = EscLang.Eval.Evaluator.Evaluate(file, programOutput);
    }
    catch (Exception e)
    {
        programOutput.WriteLine("*** CRASH! ***");
        programOutput.WriteLine(e.ToString());
        WriteLine("Failed: Crash - " + e.ToString());
        return;
    }

    if (actual != expected)
    {
        WriteLine("Failed: " + testCasePath + " - Expected: " + expected + ", Actual: " + actual);
        return;
    }

    WriteLine("Passed: " + testCasePath);
}

static EscLang.Parse.SyntaxNode NoResult() => new EscLang.Eval.Evaluator.ReturningVoidNode();
static EscLang.Parse.SyntaxNode Number(int value) => new EscLang.Parse.LiteralNumberNode(value.ToString());