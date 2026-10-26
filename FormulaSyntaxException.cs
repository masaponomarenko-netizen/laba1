using System;

namespace FormulaEvaluator;

public class FormulaSyntaxException : Exception
{
    public int Line { get; }
    
    public int Column { get; }
    
    public string? OffendingText { get; }

    public FormulaSyntaxException(string message, int line, int column, string? offendingText)
        : base($"Синтаксична помилка (рядок {line}, позиція {column}): {message}")
    {
        Line = line;
        Column = column;
        OffendingText = offendingText;
    }
}