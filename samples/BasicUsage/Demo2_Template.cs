using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 2 — Reusable template: define the schema once and apply it
/// to multiple datasets (monthly batches, regional splits, etc.).
/// </summary>
public static class Demo2_Template
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 2 — Reusable Template");
        Console.WriteLine("════════════════════════════════════════════");

        var orders = DataSeeder.SalesOrders();

        // ----------------------------------------------------------
        // 2a. Define the template ONCE
        // ----------------------------------------------------------
        var orderTemplate = ReportTemplate<SalesOrder>
            .Define("Sales Orders")
            .AddColumn("Order #",      x => x.OrderId)
            .AddColumn("Customer",     x => x.CustomerName)
            .AddColumn("Region",       x => x.Region)
            .AddColumn("Product",      x => x.Product)
            .AddColumn("Qty",          x => x.Quantity)
            .AddColumn("Unit Price",   x => x.UnitPrice)
            .AddColumn("Total",        x => x.Quantity * x.UnitPrice)
            .AddColumn("Order Date",   x => x.OrderDate.ToString("yyyy-MM-dd"))
            .AddColumn("Shipped",      x => x.IsShipped ? "Yes" : "No")
            .Build();

        // ----------------------------------------------------------
        // 2b. Apply to all orders
        // ----------------------------------------------------------
        await orderTemplate.From(orders, "All Sales Orders")
            .ToCsv("./reports/demo2_all_orders.csv")
            .ToExcel("./reports/demo2_all_orders.xlsx")
            .GenerateAsync();

        Console.WriteLine($"  [2a] All orders         → demo2_all_orders.csv + .xlsx  ({orders.Count} rows)");

        // ----------------------------------------------------------
        // 2c. Apply same template per region — no schema duplication
        // ----------------------------------------------------------
        var regions = orders.Select(o => o.Region).Distinct().OrderBy(r => r).ToList();

        foreach (var region in regions)
        {
            var regionOrders = orders.Where(o => o.Region == region).ToList();
            var fileName = $"demo2_orders_{region.ToLower()}.csv";

            await orderTemplate.From(regionOrders, $"Sales — {region}")
                .ToCsv($"./reports/{fileName}")
                .GenerateAsync();

            Console.WriteLine($"  [2b] {region,-13} region → {fileName,-38} ({regionOrders.Count} rows)");
        }

        // ----------------------------------------------------------
        // 2d. Apply same template to shipped-only subset — different title
        // ----------------------------------------------------------
        var shipped = orders.Where(o => o.IsShipped).ToList();

        await orderTemplate.From(shipped, "Shipped Orders Only")
            .ToExcel("./reports/demo2_shipped.xlsx")
            .GenerateAsync();

        Console.WriteLine($"  [2c] Shipped only       → demo2_shipped.xlsx           ({shipped.Count} rows)");
    }
}
