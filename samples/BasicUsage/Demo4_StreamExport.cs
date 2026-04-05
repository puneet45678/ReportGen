using System.Text;
using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 4 — Stream export: writing to MemoryStream instead of disk.
/// Simulates the ASP.NET "download file" pattern and shows how to
/// inspect the bytes without touching the file system.
/// </summary>
public static class Demo4_StreamExport
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 4 — Stream Export");
        Console.WriteLine("════════════════════════════════════════════");

        var employees = DataSeeder.Employees();
        var orders = DataSeeder.SalesOrders();

        // ----------------------------------------------------------
        // 4a. CSV to MemoryStream — inspect content in memory
        //     Real-world equivalent: return File(ms, "text/csv") in ASP.NET
        // ----------------------------------------------------------
        using var csvStream = new MemoryStream();

        await Report.Create("Employee Export")
            .From(employees)
            .AddColumn("Name",       x => x.Name)
            .AddColumn("Department", x => x.Department)
            .AddColumn("Email",      x => x.Email)
            .ToCsv(csvStream)
            .GenerateAsync();

        // Stream stays open — we can read it back
        csvStream.Position = 0;
        var csvText = await new StreamReader(csvStream, Encoding.UTF8).ReadToEndAsync();
        var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Console.WriteLine($"  [4a] CSV to MemoryStream:");
        Console.WriteLine($"       Total bytes : {csvStream.Length}");
        Console.WriteLine($"       Total lines : {lines.Length}  (1 header + {lines.Length - 1} data rows)");
        Console.WriteLine($"       Header row  : {lines[0].Trim()}");
        Console.WriteLine($"       First row   : {lines[1].Trim()}");

        // ----------------------------------------------------------
        // 4b. Excel to MemoryStream — save as a local file afterwards
        //     without constructing the exporter with a path
        // ----------------------------------------------------------
        using var xlsxStream = new MemoryStream();

        await Report.Create("Orders Export")
            .From(orders)
            .AddColumn("Order #",    x => x.OrderId)
            .AddColumn("Customer",   x => x.CustomerName)
            .AddColumn("Product",    x => x.Product)
            .AddColumn("Total",      x => x.Quantity * x.UnitPrice)
            .AddColumn("Date",       x => x.OrderDate.ToString("yyyy-MM-dd"))
            .ToExcel(xlsxStream)
            .GenerateAsync();

        // Caller owns the stream — write it to disk manually
        Directory.CreateDirectory("./reports");
        xlsxStream.Position = 0;
        await using var file = new FileStream("./reports/demo4_orders_stream.xlsx", FileMode.Create);
        await xlsxStream.CopyToAsync(file);

        Console.WriteLine($"  [4b] Excel to MemoryStream → flushed to demo4_orders_stream.xlsx");
        Console.WriteLine($"       Stream size: {xlsxStream.Length:N0} bytes");

        // ----------------------------------------------------------
        // 4c. Same stream, multiple reads — stream stays open (leaveOpen)
        // ----------------------------------------------------------
        xlsxStream.Position = 0;
        Console.WriteLine($"  [4c] Stream still readable after export: {xlsxStream.CanRead}");
    }
}
