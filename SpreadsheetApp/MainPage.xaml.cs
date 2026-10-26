using System.Globalization;
using System.Text;
using FormulaEvaluator;
using Microsoft.Maui.Storage;

namespace SpreadsheetApp;

public partial class MainPage : ContentPage
{
    private int _rows = 12;
    private int _cols = 8;

    // Зберігає вирази незалежно від того, що зараз показано (ВИРАЗ чи ЗНАЧЕННЯ)
    private readonly Dictionary<string, string> _expressions = new();

    private Entry? _activeCell;
    private bool _updating;      // захист від рекурсії під час програмної зміни Text
    private bool _showingValues; // true = показуємо обчислені значення

    public MainPage()
    {
        InitializeComponent();
        RebuildGrid();
    }

    private static string SavePath =>
        Path.Combine(FileSystem.AppDataDirectory, "spreadsheet.tsv");

    // ------------------------------------------------------------------
    //  ПОБУДОВА ТАБЛИЦІ
    // ------------------------------------------------------------------

    private void RebuildGrid()
    {
        _updating = true;

        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        // Колонка заголовків + колонки клітинок
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        for (int c = 0; c < _cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        // Рядок заголовків + рядки клітинок
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        for (int r = 0; r < _rows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

        // Заголовки стовпчиків: A, B, C...
        for (int c = 0; c < _cols; c++)
        {
            var header = new Label
            {
                Text = ColumnName(c),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                BackgroundColor = Color.FromArgb("#E8E8E8")
            };
            grid.Add(header, c + 1, 0);
        }

        // Заголовки рядків + клітинки
        for (int r = 0; r < _rows; r++)
        {
            var rowHeader = new Label
            {
                Text = (r + 1).ToString(CultureInfo.InvariantCulture),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                BackgroundColor = Color.FromArgb("#E8E8E8")
            };
            grid.Add(rowHeader, 0, r + 1);

            for (int c = 0; c < _cols; c++)
            {
                string cellRef = ToRef(r, c);
                var entry = new Entry
                {
                    ClassId = cellRef,
                    Text = _expressions.TryGetValue(cellRef, out var expr) ? expr : string.Empty,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 1, 1),
                    VerticalTextAlignment = TextAlignment.Center
                };
                entry.TextChanged += Cell_TextChanged;
                entry.Focused += Cell_Focused;
                grid.Add(entry, c + 1, r + 1);
            }
        }

        _updating = false;
        _showingValues = false;
        modeButton.Text = "Режим: ВИРАЗ";
    }

    private IEnumerable<Entry> Cells() => grid.Children.OfType<Entry>();

    // ------------------------------------------------------------------
    //  РЕДАГУВАННЯ КЛІТИНОК
    // ------------------------------------------------------------------

    private void Cell_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (sender is not Entry entry || entry.ClassId is not string cellRef) return;

        _expressions[cellRef] = e.NewTextValue ?? string.Empty;

        if (!_showingValues && ReferenceEquals(entry, _activeCell))
            textInput.Text = e.NewTextValue ?? string.Empty;

        if (_showingValues)
        {
            _showingValues = false;
            modeButton.Text = "Режим: ВИРАЗ";
        }
    }

    private void Cell_Focused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        _activeCell = entry;
        if (!_showingValues)
            textInput.Text = entry.Text ?? string.Empty;
    }

    private void FormulaBar_Completed(object? sender, EventArgs e)
    {
        if (_activeCell is null || _showingValues) return;
        _activeCell.Text = textInput.Text ?? string.Empty;
    }

    private void CollectExpressions()
    {
        if (_showingValues) return;
        foreach (var entry in Cells())
            if (entry.ClassId is string cellRef)
                _expressions[cellRef] = entry.Text ?? string.Empty;
    }

    // ------------------------------------------------------------------
    //  ОБЧИСЛЕННЯ
    // ------------------------------------------------------------------

    private async void CalculateButton_Clicked(object? sender, EventArgs e)
        => await CalculateAsync();

    private async Task CalculateAsync()
    {
        try
        {
            CollectExpressions();
            var values = EvaluateAll();

            _updating = true;
            foreach (var entry in Cells())
                if (entry.ClassId is string cellRef && values.TryGetValue(cellRef, out var value))
                    entry.Text = FormatNumber(value);
            _updating = false;

            _showingValues = true;
            modeButton.Text = "Режим: ЗНАЧЕННЯ";
            statusLabel.Text = "Обчислено: " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _updating = false;
            await DisplayAlertAsync("Помилка", ex.Message, "OK");
        }
    }

    private Dictionary<string, double> EvaluateAll()
    {
        var cache = new Dictionary<string, double>();
        var visiting = new HashSet<string>();
        var result = new Dictionary<string, double>();

        double Eval(string cellRef)
        {
            if (cache.TryGetValue(cellRef, out var cached))
                return cached;

            var (row, col) = ParseRef(cellRef);
            if (row < 0 || row >= _rows || col < 0 || col >= _cols)
                throw new InvalidOperationException($"Клітинка {cellRef} знаходиться поза таблицею.");

            if (!visiting.Add(cellRef))
                throw new InvalidOperationException($"Циклічне посилання на клітинку {cellRef}.");

            _expressions.TryGetValue(cellRef, out var expression);
            double value;
            try
            {
                value = Calculator.Evaluate(expression ?? string.Empty, Eval);
            }
            finally
            {
                visiting.Remove(cellRef);
            }

            cache[cellRef] = value;
            return value;
        }

        foreach (var entry in Cells())
            if (entry.ClassId is string cellRef)
                result[cellRef] = Eval(cellRef);

        return result;
    }

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "—";

        return value.ToString("0.##########", CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------
    //  РЕЖИМИ: ВИРАЗ / ЗНАЧЕННЯ
    // ------------------------------------------------------------------

    private async void ModeButton_Clicked(object? sender, EventArgs e)
    {
        if (!_showingValues)
        {
            await CalculateAsync();
            return;
        }

        _updating = true;
        foreach (var entry in Cells())
            if (entry.ClassId is string cellRef)
                entry.Text = _expressions.TryGetValue(cellRef, out var expr) ? expr : string.Empty;
        _updating = false;

        _showingValues = false;
        modeButton.Text = "Режим: ВИРАЗ";
        statusLabel.Text = "Показано вирази";
    }

    // ------------------------------------------------------------------
    //  ДОДАВАННЯ / ВИДАЛЕННЯ РЯДКІВ І СТОВПЧИКІВ
    // ------------------------------------------------------------------

    private void AddRowButton_Clicked(object? sender, EventArgs e)
    {
        _rows++;
        _activeCell = null;
        RebuildGrid();
        statusLabel.Text = $"Додано рядок. Рядків: {_rows}";
    }

    private void AddColumnButton_Clicked(object? sender, EventArgs e)
    {
        _cols++;
        _activeCell = null;
        RebuildGrid();
        statusLabel.Text = $"Додано стовпчик. Стовпчиків: {_cols}";
    }

    private void DeleteRowButton_Clicked(object? sender, EventArgs e)
    {
        if (_rows <= 1) return;

        int row = _activeCell?.ClassId is string cellRef
            ? ParseRef(cellRef).row
            : _rows - 1;

        DeleteRow(row);
    }

    private void DeleteColumnButton_Clicked(object? sender, EventArgs e)
    {
        if (_cols <= 1) return;

        int col = _activeCell?.ClassId is string cellRef
            ? ParseRef(cellRef).col
            : _cols - 1;

        DeleteColumn(col);
    }

    private void DeleteRow(int row)
    {
        var reindexed = new Dictionary<string, string>();
        foreach (var (cellRef, expr) in _expressions)
        {
            var (r, c) = ParseRef(cellRef);
            if (r == row) continue;
            if (r > row) r--;
            reindexed[ToRef(r, c)] = expr;
        }

        _expressions.Clear();
        foreach (var (key, value) in reindexed)
            _expressions[key] = value;

        _rows--;
        _activeCell = null;
        RebuildGrid();
        statusLabel.Text = $"Видалено рядок {row + 1}. Рядків: {_rows}";
    }

    private void DeleteColumn(int col)
    {
        var reindexed = new Dictionary<string, string>();
        foreach (var (cellRef, expr) in _expressions)
        {
            var (r, c) = ParseRef(cellRef);
            if (c == col) continue;
            if (c > col) c--;
            reindexed[ToRef(r, c)] = expr;
        }

        _expressions.Clear();
        foreach (var (key, value) in reindexed)
            _expressions[key] = value;

        _cols--;
        _activeCell = null;
        RebuildGrid();
        statusLabel.Text = $"Видалено стовпчик {ColumnName(col)}. Стовпчиків: {_cols}";
    }

    // ------------------------------------------------------------------
    //  ЗБЕРЕЖЕННЯ / ЧИТАННЯ ФАЙЛУ
    // ------------------------------------------------------------------

    private async void SaveButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            CollectExpressions();

            var sb = new StringBuilder();
            sb.AppendLine($"{_rows}\t{_cols}");

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (c > 0) sb.Append('\t');
                    if (_expressions.TryGetValue(ToRef(r, c), out var expr))
                        sb.Append(expr.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '));
                }
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(SavePath, sb.ToString());
            statusLabel.Text = "Збережено: " + SavePath;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Помилка збереження", ex.Message, "OK");
        }
    }

    private async void ReadButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                await DisplayAlertAsync("Читання", "Файл ще не створено. Спочатку натисніть «Зберегти».", "OK");
                return;
            }

            var lines = await File.ReadAllLinesAsync(SavePath);
            if (lines.Length == 0) return;

            var header = lines[0].Split('\t');
            _rows = Math.Max(1, int.Parse(header[0], CultureInfo.InvariantCulture));
            _cols = Math.Max(1, int.Parse(header[1], CultureInfo.InvariantCulture));

            _expressions.Clear();
            for (int r = 0; r < _rows && r + 1 < lines.Length; r++)
            {
                var parts = lines[r + 1].Split('\t');
                for (int c = 0; c < _cols && c < parts.Length; c++)
                    if (!string.IsNullOrEmpty(parts[c]))
                        _expressions[ToRef(r, c)] = parts[c];
            }

            _activeCell = null;
            RebuildGrid();
            statusLabel.Text = "Прочитано: " + SavePath;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Помилка читання", ex.Message, "OK");
        }
    }

    // ------------------------------------------------------------------
    //  ДОВІДКА ТА ВИХІД
    // ------------------------------------------------------------------

    private async void HelpButton_Clicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Довідка",
            "Електронна таблиця — лабораторна робота №1.\n\n" +
            "• Вводьте числа, вирази та посилання на клітинки (A1, B2, ...).\n" +
            "• Операції: +, -, *, /, mod, div, ^, унарні + та -.\n" +
            "• Функції: max(x,y), min(x,y).\n" +
            "• Дужки змінюють порядок обчислень.\n" +
            "• «Порахувати» обчислює всі клітинки.\n" +
            "• «Режим» перемикає ВИРАЗ / ЗНАЧЕННЯ.\n" +
            "• «Зберегти» і «Прочитати» працюють із файлом у теці застосунку.\n\n" +
            $"Файл: {SavePath}",
            "OK");
    }

    private void ExitButton_Clicked(object? sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    // ------------------------------------------------------------------
    //  ДОПОМІЖНЕ: A1 <-> (row, col)
    // ------------------------------------------------------------------

    private static string ColumnName(int index)
    {
        var sb = new StringBuilder();
        index++;
        while (index > 0)
        {
            index--;
            sb.Insert(0, (char)('A' + index % 26));
            index /= 26;
        }
        return sb.ToString();
    }

    private static (int row, int col) ParseRef(string cellRef)
    {
        int i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;

        string letters = cellRef[..i].ToUpperInvariant();
        int row = int.Parse(cellRef[i..], CultureInfo.InvariantCulture);

        int col = 0;
        foreach (char ch in letters)
            col = col * 26 + (ch - 'A' + 1);

        return (row - 1, col - 1);
    }

    private static string ToRef(int row, int col) => ColumnName(col) + (row + 1).ToString(CultureInfo.InvariantCulture);
}
