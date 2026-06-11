using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 6 — Summary rows: totals, averages, counts and custom aggregations
/// appended as a styled footer row after all data rows.
/// </summary>
public static class Demo6_SummaryRow
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 6 — Summary / Aggregate Rows");
        Console.WriteLine("════════════════════════════════════════════");

        var orders  = DataSeeder.SalesOrders();
        var employees = DataSeeder.Employees();

        // ----------------------------------------------------------
        // 6a. Sales report — Sum revenue and quantity, label the row
        // ----------------------------------------------------------
        await Report.Create("Sales Orders")
            .From(orders)
            .AddColumn("Order #",   x => x.OrderId)
            .AddColumn("Customer",  x => x.CustomerName)
            .AddColumn("Product",   x => x.Product)
            .AddColumn("Region",    x => x.Region)
            .AddColumn("Qty",       x => x.Quantity,              "#,##0")
            .AddColumn("Unit Price",x => x.UnitPrice,             "$#,##0.00")
            .AddColumn("Revenue",   x => x.Quantity * x.UnitPrice,"$#,##0.00")
            .AddSummaryRow(row => row
                .Set("Order #",    "TOTAL")
                .Sum("Qty")
                .Sum("Revenue"))
            .ToCsv("./reports/demo6_sales.csv")
            .ToExcel("./reports/demo6_sales.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [6a] Sales totals        → demo6_sales.csv + .xlsx");

        // ----------------------------------------------------------
        // 6b. Compensation report — Sum, Average, Min, Max in one row
        // ----------------------------------------------------------
        await Report.Create("Compensation Summary")
            .From(employees)
            .AddColumn("Name",        x => x.Name)
            .AddColumn("Department",  x => x.Department)
            .AddColumn("Salary",      x => x.Salary,           "$#,##0.00")
            .AddColumn("Years Exp.",  x => x.YearsOfExperience, "#,##0")
            .AddSummaryRow(row => row
                .Set("Name",       "SUMMARY")
                .Count("Department")          // how many employees
                .Sum("Salary")                // total payroll
                .Average("Years Exp."))       // avg experience
            .ToExcel("./reports/demo6_compensation.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [6b] Compensation stats  → demo6_compensation.xlsx  (Sum + Avg + Count)");

        // ----------------------------------------------------------
        // 6c. Min / Max — best and worst order by revenue
        // ----------------------------------------------------------
        await Report.Create("Revenue Range")
            .From(orders)
            .AddColumn("Product",  x => x.Product)
            .AddColumn("Region",   x => x.Region)
            .AddColumn("Revenue",  x => x.Quantity * x.UnitPrice, "$#,##0.00")
            .AddSummaryRow(row => row
                .Set("Product", "RANGE")
                .Min("Revenue")        // smallest single order
                .Max("Revenue"))       // largest single order
            .ToExcel("./reports/demo6_revenue_range.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [6c] Revenue range       → demo6_revenue_range.xlsx  (Min + Max)");

        // ----------------------------------------------------------
        // 6d. Custom Compute — shipped vs total order ratio
        // ----------------------------------------------------------
        await Report.Create("Fulfilment Report")
            .From(orders)
            .AddColumn("Order #",   x => x.OrderId)
            .AddColumn("Product",   x => x.Product)
            .AddColumn("Shipped",   x => x.IsShipped ? "Yes" : "No")
            .AddColumn("Revenue",   x => x.Quantity * x.UnitPrice, "$#,##0.00")
            .AddSummaryRow(row => row
                .Set("Order #", "TOTALS")
                .Compute("Shipped",  data =>
                {
                    // custom: "70 / 100 shipped"
                    var shipped = data.Count(o => o.IsShipped);
                    return $"{shipped} / {data.Count} shipped";
                })
                .Sum("Revenue"))
            .ToCsv("./reports/demo6_fulfilment.csv")
            .ToExcel("./reports/demo6_fulfilment.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [6d] Fulfilment report   → demo6_fulfilment.csv + .xlsx  (custom Compute)");

        // ----------------------------------------------------------
        // 6e. Reusable template — same columns, different data,
        //     summary row added per binding (not on the template itself)
        // ----------------------------------------------------------
        var orderTemplate = ReportTemplate<SalesOrder>.Define("Regional Sales")
            .AddColumn("Order #",  x => x.OrderId)
            .AddColumn("Customer", x => x.CustomerName)
            .AddColumn("Product",  x => x.Product)
            .AddColumn("Qty",      x => x.Quantity,              "#,##0")
            .AddColumn("Revenue",  x => x.Quantity * x.UnitPrice,"$#,##0.00")
            .Build();

        foreach (var region in new[] { "North", "South", "East" })
        {
            var regional = orders.Where(o => o.Region == region).ToList();
            if (regional.Count == 0) continue;

            await orderTemplate
                .From(regional, $"Sales — {region} Region")
                .AddSummaryRow(row => row          // summary added per binding
                    .Set("Order #", "TOTAL")
                    .Sum("Qty")
                    .Sum("Revenue"))
                .ToExcel($"./reports/demo6_{region.ToLower()}_sales.xlsx")
                .GenerateAsync();
        }

        Console.WriteLine("  [6e] Regional templates  → demo6_north/south/east_sales.xlsx  (template + summary)");

        // ----------------------------------------------------------
        // 6f. Multi-sheet workbook — each sheet has its own summary
        // ----------------------------------------------------------
        await new MultiSheetExcelExporter("./reports/demo6_multisheet.xlsx")
            .AddSheet("Sales", orders, b => b
                .AddColumn("Product",  x => x.Product)
                .AddColumn("Region",   x => x.Region)
                .AddColumn("Qty",      x => x.Quantity,               "#,##0")
                .AddColumn("Revenue",  x => x.Quantity * x.UnitPrice,  "$#,##0.00")
                .AddSummaryRow(row => row
                    .Set("Product", "TOTAL")
                    .Sum("Qty")
                    .Sum("Revenue")))
            .AddSheet("Employees", employees, b => b
                .AddColumn("Name",       x => x.Name)
                .AddColumn("Department", x => x.Department)
                .AddColumn("Salary",     x => x.Salary, "$#,##0.00")
                .AddSummaryRow(row => row
                    .Set("Name",   "TOTAL PAYROLL")
                    .Count("Department")
                    .Sum("Salary")))
            .WriteAsync();

        Console.WriteLine("  [6f] Multi-sheet         → demo6_multisheet.xlsx  (2 sheets each with summary)");
    }
}
