using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 5 — Edge cases: empty datasets, null values, special characters
/// in titles, deeply nested directory creation, and cancellation.
/// </summary>
public static class Demo5_EdgeCases
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 5 — Edge Cases");
        Console.WriteLine("════════════════════════════════════════════");

        // ----------------------------------------------------------
        // 5a. Empty dataset — headers still written, zero data rows
        // ----------------------------------------------------------
        var empty = Array.Empty<Employee>();

        await Report.Create("Empty Report")
            .From(empty)
            .AddColumn("Name",       x => x.Name)
            .AddColumn("Department", x => x.Department)
            .ToCsv("./reports/demo5_empty.csv")
            .ToExcel("./reports/demo5_empty.xlsx")
            .GenerateAsync();

        var emptyLines = await File.ReadAllLinesAsync("./reports/demo5_empty.csv");
        Console.WriteLine($"  [5a] Empty dataset      → demo5_empty.csv  ({emptyLines.Length} line(s): header only)");

        // ----------------------------------------------------------
        // 5b. Null values in data — exporter writes blank cell / empty field
        // ----------------------------------------------------------
        var withNulls = new[]
        {
            new { Name = "Ava Smith",   Note = (string?)"",              Score = (int?)95  },
            new { Name = "Noah Jones",  Note = (string?)"Top performer", Score = (int?)null },
            new { Name = "Mia Brown",   Note = (string?)null,            Score = (int?)null },
        };

        await Report.Create("Nullable Fields")
            .From(withNulls)
            .AddColumn("Name",  x => x.Name)
            .AddColumn("Note",  x => x.Note)   // nullable string
            .AddColumn("Score", x => x.Score)  // nullable int
            .ToCsv("./reports/demo5_nulls.csv")
            .ToExcel("./reports/demo5_nulls.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [5b] Null values        → demo5_nulls.csv + .xlsx  (nulls render as blank)");

        // ----------------------------------------------------------
        // 5c. Special characters in report title (Excel sheet name gets sanitised)
        // ----------------------------------------------------------
        await Report.Create("Q1 [Sales]: Revenue * 2 / Goals")
            .From(DataSeeder.SalesOrders().Take(10).ToList())
            .AddColumn("Order #",   x => x.OrderId)
            .AddColumn("Customer",  x => x.CustomerName)
            .AddColumn("Total",     x => x.Quantity * x.UnitPrice)
            .ToExcel("./reports/demo5_special_chars.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [5c] Special chars      → demo5_special_chars.xlsx  ([ ] : * / replaced with _)");

        // ----------------------------------------------------------
        // 5d. Long sheet title (> 31 chars) — auto truncated by ExcelExporter
        // ----------------------------------------------------------
        await Report.Create("This Is A Very Long Report Title That Exceeds The Excel Sheet Name Limit")
            .From(DataSeeder.Employees().Take(5).ToList())
            .AddColumn("Name", x => x.Name)
            .ToExcel("./reports/demo5_long_title.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [5d] Long title (>31ch) → demo5_long_title.xlsx     (sheet name truncated to 31)");

        // ----------------------------------------------------------
        // 5e. Deep nested directory — created automatically
        // ----------------------------------------------------------
        await Report.Create("Deep Directory")
            .From(DataSeeder.Employees().Take(3).ToList())
            .AddColumn("Name", x => x.Name)
            .ToCsv("./reports/sub/nested/deep/demo5_deep.csv")
            .GenerateAsync();

        Console.WriteLine("  [5e] Deep directory     → reports/sub/nested/deep/demo5_deep.csv  (auto-created)");

        // ----------------------------------------------------------
        // 5f. Cancellation — token cancelled before export starts
        // ----------------------------------------------------------
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await Report.Create("Cancelled")
                .From(DataSeeder.Employees())
                .AddColumn("Name", x => x.Name)
                .ToCsv("./reports/demo5_cancelled.csv")
                .GenerateAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  [5f] Cancellation       → OperationCanceledException caught as expected");
        }

        // ----------------------------------------------------------
        // 5g. All supported value types in a single report
        //     Verifies SetCellValue handles them without falling back to .ToString()
        // ----------------------------------------------------------
        var typedData = new[]
        {
            new
            {
                IntVal     = 42,
                LongVal    = 9_000_000_000L,
                ShortVal   = (short)7,
                ByteVal    = (byte)255,
                UintVal    = (uint)3_000_000_000u,
                FloatVal   = 3.14f,
                DoubleVal  = 2.718281828,
                DecimalVal = 99.99m,
                BoolVal    = true,
                DateVal    = new DateOnly(2026, 4, 2),
                TimeVal    = new TimeOnly(14, 30, 0),
                GuidVal    = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            }
        };

        await Report.Create("All Value Types")
            .From(typedData)
            .AddColumn("Int",     x => x.IntVal)
            .AddColumn("Long",    x => x.LongVal)
            .AddColumn("Short",   x => x.ShortVal)
            .AddColumn("Byte",    x => x.ByteVal)
            .AddColumn("Uint",    x => x.UintVal)
            .AddColumn("Float",   x => x.FloatVal)
            .AddColumn("Double",  x => x.DoubleVal)
            .AddColumn("Decimal", x => x.DecimalVal)
            .AddColumn("Bool",    x => x.BoolVal)
            .AddColumn("Date",    x => x.DateVal)
            .AddColumn("Time",    x => x.TimeVal)
            .AddColumn("Guid",    x => x.GuidVal)
            .ToCsv("./reports/demo5_all_types.csv")
            .ToExcel("./reports/demo5_all_types.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [5g] All value types    → demo5_all_types.csv + .xlsx");
    }
}
