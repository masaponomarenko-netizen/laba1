# Лабораторна робота №1 — Електронна таблиця

Застосунок електронної таблиці на **.NET MAUI (Mac Catalyst)** з власним обчислювачем
формул на **ANTLR4**.

## Варіант

Підтримувані операції: `+`, `-`, `*`, `/`, `mod`, `div`, унарні `+` і `-`, `^`,
а також функції `max(x, y)` і `min(x, y)`.

## Структура проєкту

```
.
├── FormulaEvaluator.csproj        # бібліотека класів (.NET 8)
├── FormulaGrammar.g4              # граматика ANTLR4
├── FormulaVisitor.cs              # обчислення дерева розбору
├── Calculator.cs                  # зручний API: Evaluate / TryEvaluate
├── FormulaSyntaxException.cs      # помилка синтаксису з позицією
├── SpreadsheetApp/                # MAUI-застосунок (Mac Catalyst, .NET 10)
│   ├── MainPage.xaml              # інтерфейс таблиці
│   ├── MainPage.xaml.cs           # логіка таблиці
│   └── Platforms/MacCatalyst/     # Info.plist, AppDelegate, SceneDelegate
└── laba1.sln
```

## Вимоги

- .NET SDK 8.0+ (для бібліотеки) та .NET 10 SDK з workload `maui-maccatalyst`
- Xcode (для збірки Mac Catalyst)
- macOS

## Збірка та запуск

```bash
dotnet workload install maui-maccatalyst
dotnet build SpreadsheetApp/SpreadsheetApp.csproj -f net10.0-maccatalyst
open SpreadsheetApp/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/SpreadsheetApp.app
```

Або однією командою:

```bash
dotnet build SpreadsheetApp/SpreadsheetApp.csproj -f net10.0-maccatalyst -t:Run
```

## Можливості

- клітинки у форматі `A1`, `B2`, `AA10`;
- обчислення виразів із посиланнями на інші клітинки;
- автоматичне виявлення циклічних посилань;
- режими **ВИРАЗ** / **ЗНАЧЕННЯ**;
- додавання та видалення рядків і стовпчиків;
- збереження та читання таблиці з файлу `spreadsheet.tsv`;
- вбудована довідка.

## Приклади

| Вираз              | Результат |
|--------------------|-----------|
| `2+3*4`            | `14`      |
| `(2+3)*4`          | `20`      |
| `2^3^2`            | `512`     |
| `10 mod 3`         | `1`       |
| `10 div 3`         | `3`       |
| `max(A1, B1) + 1`  | залежить від клітинок |
| `min(A1, 2) * 3`   | залежить від клітинок |

## Обчислювач формул (ДЗ5)

`Calculator.Evaluate(string expression, Func<string, double> resolveCell)` приймає
вираз і функцію розв'язання посилань на клітинки, тому той самий обчислювач
використовується і в консольних тестах, і в MAUI-застосунку.

Провідний знак `=` у формулі необов'язковий: `=A1+B1` і `A1+B1` еквівалентні.
