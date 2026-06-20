using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 7 — PDF export: generating rich, paginated PDF reports using QuestPDF.
/// Covers default options, custom layout/colours, summary rows, and
/// combining PDF with CSV and Excel in a single GenerateAsync() call.
/// </summary>
public static class Demo7_PdfExport
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 7 — PDF Export");
        Console.WriteLine("════════════════════════════════════════════");

        var employees = DataSeeder.Employees();
        var orders    = DataSeeder.SalesOrders();

        // ----------------------------------------------------------
        // 7a. Basic PDF — all defaults (A4 portrait, blue header,
        //     title, alternating rows, page numbers + timestamp)
        // ----------------------------------------------------------
        await Report.Create("Employee Directory")
            .From(employees)
            .AddColumn("Name",       x => x.Name)
            .AddColumn("Department", x => x.Department)
            .AddColumn("Email",      x => x.Email)
            .AddColumn("Salary",     x => x.Salary)
            .AddColumn("Active",     x => x.IsActive ? "Yes" : "No")
            .ToPdf("./reports/demo7_employees.pdf")
            .GenerateAsync();

        Console.WriteLine("  [7a] Employee directory  → demo7_employees.pdf  (A4 portrait, defaults)");

        // ----------------------------------------------------------
        // 7b. Custom options — Letter landscape, teal header,
        //     no footer timestamp (useful for internal drafts)
        // ----------------------------------------------------------
        await Report.Create("Sales Orders — Q1")
            .From(orders)
            .AddColumn("Order #",   x => x.OrderId)
            .AddColumn("Customer",  x => x.CustomerName)
            .AddColumn("Region",    x => x.Region)
            .AddColumn("Product",   x => x.Product)
            .AddColumn("Qty",       x => x.Quantity)
            .AddColumn("Revenue",   x => x.Quantity * x.UnitPrice)
            .AddColumn("Shipped",   x => x.IsShipped ? "Yes" : "No")
            .ToPdf("./reports/demo7_sales_landscape.pdf", new PdfExportOptions
            {
                PageSize              = PdfPageSize.Letter,
                Landscape             = true,
                HeaderBackgroundColor = "#0F766E",  // teal-700
                HeaderTextColor       = "#F0FDFA",
                AlternateRowColor     = "#F0FDFA",
                ShowGeneratedAt       = false        // suppress timestamp
            })
            .GenerateAsync();

        Console.WriteLine("  [7b] Sales orders        → demo7_sales_landscape.pdf  (Letter landscape, teal)");

        // ----------------------------------------------------------
        // 7c. Summary row in PDF — totals + aggregate footer cell
        //     appears bold with a separator line above it
        // ----------------------------------------------------------
        await Report.Create("Compensation Report")
            .From(employees)
            .AddColumn("Name",       x => x.Name)
            .AddColumn("Department", x => x.Department)
            .AddColumn("Salary",     x => x.Salary)
            .AddColumn("Yrs Exp.",   x => x.YearsOfExperience)
            .AddSummaryRow(row => row
                .Set("Name",       "SUMMARY")
                .Count("Department")      // headcount
                .Sum("Salary")            // total payroll
                .Average("Yrs Exp."))     // avg experience
            .ToPdf("./reports/demo7_compensation.pdf")
            .GenerateAsync();

        Console.WriteLine("  [7c] Compensation totals → demo7_compensation.pdf  (with summary row)");

        // ----------------------------------------------------------
        // 7d. Triple export — one chain produces CSV + Excel + PDF
        //     Same ReportDefinition snapshot is passed to all three
        // ----------------------------------------------------------
        await Report.Create("Full Sales Report")
            .From(orders)
            .AddColumn("Order #",    x => x.OrderId)
            .AddColumn("Customer",   x => x.CustomerName)
            .AddColumn("Product",    x => x.Product)
            .AddColumn("Region",     x => x.Region)
            .AddColumn("Unit Price", x => x.UnitPrice)
            .AddColumn("Qty",        x => x.Quantity)
            .AddColumn("Revenue",    x => x.Quantity * x.UnitPrice)
            .AddSummaryRow(row => row
                .Set("Order #", "TOTAL")
                .Sum("Qty")
                .Sum("Revenue"))
            .ToCsv  ("./reports/demo7_full_sales.csv")
            .ToExcel("./reports/demo7_full_sales.xlsx")
            .ToPdf  ("./reports/demo7_full_sales.pdf", new PdfExportOptions
            {
                PageSize  = PdfPageSize.A3,
                Landscape = true
            })
            .GenerateAsync();

        Console.WriteLine("  [7d] Full sales (triple) → demo7_full_sales.csv + .xlsx + .pdf  (A3 landscape)");
    }
}
