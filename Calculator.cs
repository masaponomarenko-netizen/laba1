using Antlr4.Runtime;
using System;
using System.IO;

namespace FormulaEvaluator;

public class ThrowErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, 
        int charPositionInLine, string msg, RecognitionException e)
    {
        throw new FormulaSyntaxException(msg, line, charPositionInLine + 1, offendingSymbol?.Text);
    }
    
    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, 
        int charPositionInLine, string msg, RecognitionException e)
    {
        throw new FormulaSyntaxException(msg, line, charPositionInLine + 1, null);
    }
}

public static class Calculator
{

    public static double Evaluate(string expression, Func<string, double> getCellValue)
    {
        if (string.IsNullOrWhiteSpace(expression)) return 0;

        expression = Normalize(expression);

        var lexer = new FormulaGrammarLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new ThrowErrorListener());

        var parser = new FormulaGrammarParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ThrowErrorListener());

        var tree = parser.compileUnit();
        var visitor = new FormulaVisitor(getCellValue);

        return visitor.Visit(tree);
    }

    internal static string Normalize(string expression)
    {
        expression = expression.Trim();
        if (expression.StartsWith("="))
        {
            expression = expression.Substring(1);
        }
        return expression;
    }
}
