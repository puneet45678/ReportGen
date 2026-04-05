using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 3 — Attribute-based column discovery: columns declared on the
/// model class via [ReportColumn]. No .AddColumn() calls needed.
/// Also shows mixing attributes with manual columns.
/// </summary>
public static class Demo3_AttributeDiscovery
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 3 — Attribute Discovery");
        Console.WriteLine("════════════════════════════════════════════");

        var products = DataSeeder.ProductSnapshots();

        // ----------------------------------------------------------
        // 3a. Pure attribute discovery — zero manual .AddColumn() calls
        //     Order= on [ReportColumn] controls column sequence
        // ----------------------------------------------------------
        await Report.Create("Product Catalog")
            .From(products)
            .AddColumnsFromAttributes()       // discovers all [ReportColumn] props
            .ToCsv("./reports/demo3_catalog.csv")
            .ToExcel("./reports/demo3_catalog.xlsx")
            .GenerateAsync();

        Console.WriteLine($"  [3a] Pure attributes    → demo3_catalog.csv + .xlsx  ({products.Count} rows)");
        Console.WriteLine("       Columns: Product ID, Product Name, Category, Unit Price, Stock, Last Restocked");
        Console.WriteLine("       'InternalCode' property has no [ReportColumn] → excluded automatically");

        // ----------------------------------------------------------
        // 3b. Mixed: attributes + a computed column added manually
        //     The manual column appends AFTER the attribute-discovered ones
        // ----------------------------------------------------------
        await Report.Create("Product Valuation")
            .From(products)
            .AddColumnsFromAttributes()
            .AddColumn("Stock Value", x => x.UnitPrice * x.StockLevel)  // computed — not on the model
            .ToCsv("./reports/demo3_valuation.csv")
            .GenerateAsync();

        Console.WriteLine("  [3b] Mixed              → demo3_valuation.csv  (attributes + manual 'Stock Value')");

        // ----------------------------------------------------------
        // 3c. Template with attribute discovery — reusable across datasets
        // ----------------------------------------------------------
        var productTemplate = ReportTemplate<ProductSnapshot>
            .Define("Products")
            .AddColumnsFromAttributes()
            .Build();

        var electronics = products.Where(p => p.Category == "Electronics").ToList();
        var furniture   = products.Where(p => p.Category == "Furniture").ToList();

        if (electronics.Count > 0)
        {
            await productTemplate.From(electronics, "Electronics Catalog")
                .ToCsv("./reports/demo3_electronics.csv")
                .GenerateAsync();
            Console.WriteLine($"  [3c] Electronics        → demo3_electronics.csv  ({electronics.Count} rows)");
        }

        if (furniture.Count > 0)
        {
            await productTemplate.From(furniture, "Furniture Catalog")
                .ToCsv("./reports/demo3_furniture.csv")
                .GenerateAsync();
            Console.WriteLine($"  [3c] Furniture          → demo3_furniture.csv    ({furniture.Count} rows)");
        }
    }
}
