using System.Globalization;
using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

/// <summary>
/// End-to-end integration tests: exercises the full pipeline
/// from Report.Create() through to verifying file output on disk.
/// </summary>
public sealed class IntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_Integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─── Domain models ───────────────────────────────────────────────────────

    private record Employee(string FirstName, string LastName, string Department,
        decimal Salary, int YearsOfService, bool IsActive, DateTime HireDate);

    private record Order(int OrderId, string Customer, decimal Amount,
        string Status, DateTimeOffset PlacedAt);

    private class ProductDto
    {
        [ReportColumn("Product Name", Order = 0)]
        public string Name { get; set; } = "";

        [ReportColumn("Unit Price", Order = 1)]
        public decimal Price { get; set; }

        [ReportColumn("In Stock", Order = 2)]
        public bool Available { get; set; }

        public string InternalSku { get; set; } = "";  // excluded — no attribute
    }

    private static readonly Employee[] Employees =
    [
        new("Ava",  "Patel",   "Engineering", 95_000m, 3,  true,  new DateTime(2023, 1, 10)),
        new("Noah", "Kim",     "Marketing",   72_000m, 5,  true,  new DateTime(2021, 6, 1)),
        new("Zoe",  "Sharma",  "Engineering", 105_000m, 7, true,  new DateTime(2019, 3, 15)),
        new("Liam", "Santos",  "HR",          68_000m, 1,  false, new DateTime(2025, 7, 20)),
    ];

    private static readonly Order[] Orders =
    [
        new(1001, "Acme Corp",    1_250.00m, "Shipped",   new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)),
        new(1002, "Globex Ltd",   3_400.75m, "Pending",   new DateTimeOffset(2026, 3, 5, 14, 30, 0, TimeSpan.Zero)),
        new(1003, "Initech",        800.00m, "Cancelled", new DateTimeOffset(2026, 3, 8, 11, 0, 0, TimeSpan.Zero)),
    ];

    private string File(string name) => Path.Combine(_tempDir, name);

    // ─── 1. Basic fluent builder → CSV ────────────────────────────────────────

    [Fact]
    public async Task E2E_BasicFluentBuilder_ProducesCorrectCsv()
    {
        var path = File("employees.csv");

        await Report.Create("Employees")
            .From(Employees)
            .AddColumn("First Name",  x => x.FirstName)
            .AddColumn("Last Name",   x => x.LastName)
            .AddColumn("Department",  x => x.Department)
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);

        lines[0].Should().Be("First Name,Last Name,Department");
        lines[1].Should().Be("Ava,Patel,Engineering");
        lines[2].Should().Be("Noah,Kim,Marketing");
        lines[3].Should().Be("Zoe,Sharma,Engineering");
        lines[4].Should().Be("Liam,Santos,HR");
        lines.Should().HaveCount(5); // 1 header + 4 data rows
    }

    // ─── 2. Basic fluent builder → Excel ──────────────────────────────────────

    [Fact]
    public async Task E2E_BasicFluentBuilder_ProducesCorrectExcel()
    {
        var path = File("employees.xlsx");

        await Report.Create("Employees")
            .From(Employees)
            .AddColumn("First Name",  x => x.FirstName)
            .AddColumn("Last Name",   x => x.LastName)
            .AddColumn("Salary",      x => x.Salary)
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();

        ws.Cell(1, 1).GetString().Should().Be("First Name");
        ws.Cell(1, 2).GetString().Should().Be("Last Name");
        ws.Cell(1, 3).GetString().Should().Be("Salary");

        ws.Cell(2, 1).GetString().Should().Be("Ava");
        ws.Cell(2, 3).GetValue<decimal>().Should().Be(95_000m);

        ws.RowsUsed().Count().Should().Be(5); // 1 header + 4 rows
    }

    // ─── 3. Multi-exporter: CSV + Excel from one report ───────────────────────

    [Fact]
    public async Task E2E_MultiExporter_BothFilesPresentAndConsistent()
    {
        var csvPath  = File("orders.csv");
        var xlsxPath = File("orders.xlsx");

        await Report.Create("Orders")
            .From(Orders)
            .AddColumn("Order ID",  x => x.OrderId)
            .AddColumn("Customer",  x => x.Customer)
            .AddColumn("Amount",    x => x.Amount)
            .AddColumn("Status",    x => x.Status)
            .ToCsv(csvPath)
            .ToExcel(xlsxPath)
            .GenerateAsync();

        // Both files written
        System.IO.File.Exists(csvPath).Should().BeTrue();
        System.IO.File.Exists(xlsxPath).Should().BeTrue();

        // CSV first data row
        var csvLines = await System.IO.File.ReadAllLinesAsync(csvPath);
        csvLines[1].Should().Contain("1001");
        csvLines[1].Should().Contain("Acme Corp");

        // Excel same data
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 2).GetString().Should().Be("Acme Corp");
        ws.Cell(2, 3).GetValue<decimal>().Should().Be(1_250.00m);
    }

    // ─── 4. Computed / formatted columns via accessor lambda ──────────────────

    [Fact]
    public async Task E2E_FormattedColumns_AccessorHandlesAllFormatting()
    {
        var path = File("formatted.csv");

        await Report.Create("Formatted Employees")
            .From(Employees)
            .AddColumn("Full Name",    x => $"{x.FirstName} {x.LastName}")
            .AddColumn("Salary",       x => x.Salary.ToString("C0", CultureInfo.InvariantCulture))
            .AddColumn("Hire Date",    x => x.HireDate.ToString("yyyy-MM-dd"))
            .AddColumn("Status",       x => x.IsActive ? "Active" : "Inactive")
            .AddColumn("Senior?",      x => x.YearsOfService >= 5 ? "Yes" : "No")
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);

        lines[0].Should().Be("Full Name,Salary,Hire Date,Status,Senior?");
        lines[1].Should().Be("Ava Patel,\"¤95,000\",2023-01-10,Active,No");
        lines[2].Should().Be("Noah Kim,\"¤72,000\",2021-06-01,Active,Yes");
        lines[4].Should().Be("Liam Santos,\"¤68,000\",2025-07-20,Inactive,No");
    }

    // ─── 5. Nested property access ────────────────────────────────────────────

    [Fact]
    public async Task E2E_NestedPropertyAccess_WorksViaLambda()
    {
        var data = new[]
        {
            new { Name = "Ava",  Address = new { City = "Mumbai",  Country = "India" } },
            new { Name = "Noah", Address = new { City = "Bengaluru", Country = "India" } },
        };

        var path = File("nested.csv");

        await Report.Create("Locations")
            .From(data)
            .AddColumn("Name",    x => x.Name)
            .AddColumn("City",    x => x.Address.City)
            .AddColumn("Country", x => x.Address.Country)
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);
        lines[0].Should().Be("Name,City,Country");
        lines[1].Should().Be("Ava,Mumbai,India");
    }

    // ─── 6. Attribute-based column discovery → CSV ────────────────────────────

    [Fact]
    public async Task E2E_AttributeDiscovery_ExcludesUndecorated_WritesCorrectCsv()
    {
        var data = new[]
        {
            new ProductDto { Name = "Widget", Price = 19.99m, Available = true,  InternalSku = "W-001" },
            new ProductDto { Name = "Gadget", Price = 49.50m, Available = false, InternalSku = "G-002" },
        };

        var path = File("products.csv");

        await Report.Create("Products")
            .From(data)
            .AddColumnsFromAttributes()
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);

        lines[0].Should().Be("Product Name,Unit Price,In Stock");  // InternalSku excluded
        lines[1].Should().Contain("Widget");
        lines[1].Should().NotContain("W-001");                      // SKU not in output
    }

    // ─── 7. Attribute-based → Excel ───────────────────────────────────────────

    [Fact]
    public async Task E2E_AttributeDiscovery_ProducesCorrectExcelFile()
    {
        var data = new[]
        {
            new ProductDto { Name = "Widget", Price = 19.99m, Available = true },
        };

        var path = File("products.xlsx");

        await Report.Create("Products")
            .From(data)
            .AddColumnsFromAttributes()
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(1, 1).GetString().Should().Be("Product Name");
        ws.Cell(1, 2).GetString().Should().Be("Unit Price");
        ws.Cell(1, 3).GetString().Should().Be("In Stock");
        ws.Cell(2, 1).GetString().Should().Be("Widget");
    }

    // ─── 8. Mixed: attributes + manual extra column ───────────────────────────

    [Fact]
    public async Task E2E_Mixed_AttributesPlusManualColumn_CombineCorrectly()
    {
        var data = new[]
        {
            new ProductDto { Name = "Widget", Price = 9.99m,  Available = true  },
            new ProductDto { Name = "Gadget", Price = 49.50m, Available = false },
        };

        var path = File("mixed.csv");

        await Report.Create("Mixed")
            .From(data)
            .AddColumnsFromAttributes()
            .AddColumn("Label", x => x.Available ? "SELL" : "OOS")
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);
        lines[0].Should().Be("Product Name,Unit Price,In Stock,Label");
        lines[1].Should().EndWith("SELL");
        lines[2].Should().EndWith("OOS");
    }

    // ─── 9. Reusable template: same columns, different data → CSV ─────────────

    [Fact]
    public async Task E2E_Template_SameColumns_DifferentData_IndependentFiles()
    {
        var template = ReportTemplate<Order>.Define("Monthly Orders")
            .AddColumn("Order ID",  x => x.OrderId)
            .AddColumn("Customer",  x => x.Customer)
            .AddColumn("Amount",    x => x.Amount)
            .Build();

        var marchOrders = Orders.Where(o => o.PlacedAt.Month == 3).ToArray();
        var aprilOrders = new[]
        {
            new Order(2001, "Umbrella Corp", 9_999m, "Shipped", new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.Zero))
        };

        var marchPath = File("march-orders.csv");
        var aprilPath = File("april-orders.csv");

        await template.From(marchOrders, "March Orders").ToCsv(marchPath).GenerateAsync();
        await template.From(aprilOrders, "April Orders").ToCsv(aprilPath).GenerateAsync();

        var marchLines = await System.IO.File.ReadAllLinesAsync(marchPath);
        var aprilLines = await System.IO.File.ReadAllLinesAsync(aprilPath);

        // Same header in both
        marchLines[0].Should().Be("Order ID,Customer,Amount");
        aprilLines[0].Should().Be("Order ID,Customer,Amount");

        // Different row counts
        marchLines.Should().HaveCount(4);  // header + 3 march orders
        aprilLines.Should().HaveCount(2);  // header + 1 april order

        aprilLines[1].Should().Contain("Umbrella Corp");
    }

    // ─── 10. Template: extra column added per-use doesn't leak across bindings ─

    [Fact]
    public async Task E2E_Template_ExtraColumnPerUse_DoesNotLeakToOtherBindings()
    {
        var template = ReportTemplate<Employee>.Define("Employees")
            .AddColumn("Name",       x => $"{x.FirstName} {x.LastName}")
            .AddColumn("Department", x => x.Department)
            .Build();

        var withExtraPath  = File("with-extra.csv");
        var withoutExtraPath = File("without-extra.csv");

        // First binding — adds an extra column
        await template.From(Employees)
            .AddColumn("Salary", x => x.Salary)
            .ToCsv(withExtraPath)
            .GenerateAsync();

        // Second binding — only template columns
        await template.From(Employees).ToCsv(withoutExtraPath).GenerateAsync();

        var withExtraLines   = await System.IO.File.ReadAllLinesAsync(withExtraPath);
        var withoutExtraLines = await System.IO.File.ReadAllLinesAsync(withoutExtraPath);

        withExtraLines[0].Should().Be("Name,Department,Salary");
        withoutExtraLines[0].Should().Be("Name,Department");  // Salary didn't leak
    }

    // ─── 11. Template with attribute-based columns ────────────────────────────

    [Fact]
    public async Task E2E_Template_AttributeColumns_WorksWithMultipleBindings()
    {
        var template = ReportTemplate<ProductDto>.Define("Products")
            .AddColumnsFromAttributes()
            .Build();

        var batch1 = new[] { new ProductDto { Name = "Widget", Price = 9.99m, Available = true } };
        var batch2 = new[] { new ProductDto { Name = "Gadget", Price = 49m,   Available = false } };

        var path1 = File("batch1.csv");
        var path2 = File("batch2.csv");

        await template.From(batch1).ToCsv(path1).GenerateAsync();
        await template.From(batch2).ToCsv(path2).GenerateAsync();

        var lines1 = await System.IO.File.ReadAllLinesAsync(path1);
        var lines2 = await System.IO.File.ReadAllLinesAsync(path2);

        lines1[0].Should().Be("Product Name,Unit Price,In Stock");
        lines2[0].Should().Be("Product Name,Unit Price,In Stock");
        lines1[1].Should().Contain("Widget");
        lines2[1].Should().Contain("Gadget");
    }

    // ─── 12. Empty dataset writes only header ─────────────────────────────────

    [Fact]
    public async Task E2E_EmptyDataset_WritesHeaderOnly()
    {
        var path = File("empty.csv");

        await Report.Create("Empty")
            .From(Array.Empty<Employee>())
            .AddColumn("Name", x => x.FirstName)
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);
        lines.Should().HaveCount(1);
        lines[0].Should().Be("Name");
    }

    // ─── 13. Output directory auto-created ────────────────────────────────────

    [Fact]
    public async Task E2E_OutputDirectory_CreatedIfNotExisting()
    {
        var deepPath = Path.Combine(_tempDir, "reports", "2026", "q1", "summary.csv");

        await Report.Create("Summary")
            .From(Employees)
            .AddColumn("Name", x => x.FirstName)
            .ToCsv(deepPath)
            .GenerateAsync();

        System.IO.File.Exists(deepPath).Should().BeTrue();
    }

    // ─── 14. Build-only path: verify definition without writing files ──────────

    [Fact]
    public void E2E_Build_ProducesCorrectDefinitionWithoutFiles()
    {
        var definition = Report.Create("Headcount")
            .From(Employees)
            .AddColumn("Name",       x => $"{x.FirstName} {x.LastName}")
            .AddColumn("Department", x => x.Department)
            .AddColumn("Salary",     x => x.Salary)
            .Build();

        definition.Title.Should().Be("Headcount");
        definition.Columns.Should().HaveCount(3);
        definition.Data.Should().HaveCount(4);
        definition.Columns[0].Header.Should().Be("Name");
        definition.Columns[0].Accessor(Employees[0]).Should().Be("Ava Patel");
    }

    // ─── 15. Cancellation mid-run is respected ────────────────────────────────

    [Fact]
    public async Task E2E_Cancellation_StopsBeforeExport()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => Report.Create("Cancelled")
            .From(Employees)
            .AddColumn("Name", x => x.FirstName)
            .ToCsv(File("cancelled.csv"))
            .GenerateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ─── 16. LINQ query as data source (lazy → materialised on Build) ─────────

    [Fact]
    public async Task E2E_LinqDataSource_MaterialisedCorrectly()
    {
        var path = File("filtered.csv");

        // Where + Select — not yet executed when passed to From()
        var query = Employees
            .Where(e => e.Department == "Engineering")
            .Select(e => new { Name = $"{e.FirstName} {e.LastName}", e.Salary });

        await Report.Create("Engineers")
            .From(query)
            .AddColumn("Name",   x => x.Name)
            .AddColumn("Salary", x => x.Salary)
            .ToCsv(path)
            .GenerateAsync();

        var lines = await System.IO.File.ReadAllLinesAsync(path);
        lines.Should().HaveCount(3);   // header + Ava + Zoe
        lines[1].Should().Contain("Ava Patel");
        lines[2].Should().Contain("Zoe Sharma");
    }
}
