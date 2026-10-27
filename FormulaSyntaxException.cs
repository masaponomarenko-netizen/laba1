using System;

namespace FormulaEvaluator;

public class FormulaSyntaxException : Exception
{
    public FormulaSyntaxException(string message, int line, int column)
        : base($"Синтаксична помилка (рядок {line}, позиція {column}): {message}")
    {
    }
}
