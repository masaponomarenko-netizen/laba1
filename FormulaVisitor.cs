using System;
using System.Globalization;

namespace FormulaEvaluator;

public class FormulaVisitor : FormulaGrammarBaseVisitor<double>
{
    private readonly Func<string, double> _getCellValue;

    public FormulaVisitor(Func<string, double> getCellValue)
    {
        _getCellValue = getCellValue;
    }

    public override double VisitCompileUnit(FormulaGrammarParser.CompileUnitContext context)
    {
        return Visit(context.expr());
    }

    public override double VisitNumberExpr(FormulaGrammarParser.NumberExprContext context)
    {
        return double.Parse(context.NUMBER().GetText(), CultureInfo.InvariantCulture);
    }

    public override double VisitCellExpr(FormulaGrammarParser.CellExprContext context)
    {
        string cellName = context.IDENTIFIER().GetText().ToUpper();
        return _getCellValue(cellName);
    }

    public override double VisitParenthesesExpr(FormulaGrammarParser.ParenthesesExprContext context)
    {
        return Visit(context.expr());
    }

    public override double VisitUnaryExpr(FormulaGrammarParser.UnaryExprContext context)
    {
        double value = Visit(context.expr());
        return context.op.Text == "-" ? -value : value;
    }

    public override double VisitPowerExpr(FormulaGrammarParser.PowerExprContext context)
    {
        double left = Visit(context.left);
        double right = Visit(context.right);
        return Math.Pow(left, right);
    }

    public override double VisitMultiplicativeExpr(FormulaGrammarParser.MultiplicativeExprContext context)
    {
        double left = Visit(context.left);
        double right = Visit(context.right);
        string op = context.op.Text.ToLower();

        return op switch
        {
            "*" => left * right,
            "/" => right != 0 ? left / right : throw new DivideByZeroException("Ділення на нуль"),
            "mod" => right != 0 ? left % right : throw new DivideByZeroException("Ділення на нуль (mod)"),
            "div" => right != 0 ? Math.Truncate(left / right) : throw new DivideByZeroException("Ділення на нуль (div)"),
            _ => throw new InvalidOperationException($"Невідомий оператор: {op}")
        };
    }

    public override double VisitAdditiveExpr(FormulaGrammarParser.AdditiveExprContext context)
    {
        double left = Visit(context.left);
        double right = Visit(context.right);
        return context.op.Text == "+" ? left + right : left - right;
    }

    public override double VisitMaxExpr(FormulaGrammarParser.MaxExprContext context)
    {
        double left = Visit(context.expr(0));
        double right = Visit(context.expr(1));
        return Math.Max(left, right);
    }

    public override double VisitMinExpr(FormulaGrammarParser.MinExprContext context)
    {
        double left = Visit(context.expr(0));
        double right = Visit(context.expr(1));
        return Math.Min(left, right);
    }
}