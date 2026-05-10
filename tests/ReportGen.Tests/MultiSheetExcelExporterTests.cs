using System.Globalization;
using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class MultiSheetExcelExporterTests : IDisposable
{
    private readonly string _tempDir;

    public MultiSheetExcelExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_MultiSheetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "out.xlsx") => Path.Combine(_tempDir, name);

    // ---- Test models ----

    private record Sale(string Product, decimal Revenue);
    private record Expense(string Category, decimal Amount);
    private record Summary(string Label, decimal Total);

    private static readonly Sale[] SalesData =
    [
        new("Widget", 1000m),
        new("Gadget", 2500m)
    ];

    private static readonly Expense[] ExpenseData =
    [
        new("Rent", 800m),
        new("Salaries", 5000m)
    ];

    // ---- F3-01: Two sheets with different types ----

    [Fact]
    public async Task WriteAsync_TwoSheets_DifferentTypes_BothWritten()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue))
            .AddSheet("Expenses", ExpenseData, b => b
                .AddColumn("Category", x => x.Category)
                .AddColumn("Amount", x => x.Amount))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.Should().HaveCount(2);
        wb.Worksheets.ElementAt(0).Name.Should().Be("Sales");
        wb.Worksheets.ElementAt(1).Name.Should().Be("Expenses");
    }

    // ---- F3-02: Each sheet has the right headers and data ----

    [Fact]
    public async Task WriteAsync_EachSheet_HasCorrectHeadersAndData()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue))
            .AddSheet("Expenses", ExpenseData, b => b
                .AddColumn("Category", x => x.Category)
                .AddColumn("Amount", x => x.Amount))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var sales = wb.Worksheets.ElementAt(0);
        sales.Cell(1, 1).GetString().Should().Be("Product");
        sales.Cell(1, 2).GetString().Should().Be("Revenue");
        sales.Cell(2, 1).GetString().Should().Be("Widget");
        sales.Cell(2, 2).GetValue<decimal>().Should().Be(1000m);
        sales.Cell(3, 1).GetString().Should().Be("Gadget");

        var expenses = wb.Worksheets.ElementAt(1);
        expenses.Cell(1, 1).GetString().Should().Be("Category");
        expenses.Cell(2, 1).GetString().Should().Be("Rent");
        expenses.Cell(2, 2).GetValue<decimal>().Should().Be(800m);
    }

    // ---- F3-03: Headers are bold ----

    [Fact]
    public async Task WriteAsync_Headers_AreBold()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b.AddColumn("Product", x => x.Product))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    // ---- F3-04: Sheet name is sanitized ----

    [Fact]
    public async Task WriteAsync_SheetNameSanitized_SpecialCharsReplaced()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Q1 [Sales]: Rev*", SalesData, b => b.AddColumn("P", x => x.Product))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var name = wb.Worksheets.First().Name;
        name.Should().NotContainAny("[", "]", ":", "*");
    }

    // ---- F3-05: Sheet name truncated to 31 chars ----

    [Fact]
    public async Task WriteAsync_SheetNameTruncatedTo31Chars()
    {
        var longTitle = new string('X', 40);
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet(longTitle, SalesData, b => b.AddColumn("P", x => x.Product))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Name.Length.Should().BeLessThanOrEqualTo(31);
    }

    // ---- F3-06: Duplicate sanitized sheet names throw at WriteAsync ----

    [Fact]
    public async Task WriteAsync_DuplicateSheetNames_Throws()
    {
        var path = TempFile();

        var act = async () => await new MultiSheetExcelExporter(path)
            .AddSheet("Q1 Sales", SalesData, b => b.AddColumn("P", x => x.Product))
            .AddSheet("Q1 Sales", ExpenseData, b => b.AddColumn("C", x => x.Category))
            .WriteAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicated*");
    }

    // ---- F3-07: Zero sheets throws at WriteAsync ----

    [Fact]
    public async Task WriteAsync_NoSheets_Throws()
    {
        var path = TempFile();

        var act = async () => await new MultiSheetExcelExporter(path).WriteAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least one sheet*");
    }

    // ---- F3-08: Empty sheet (no data rows) writes header only ----

    [Fact]
    public async Task WriteAsync_EmptySheet_WritesHeaderRowOnly()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Empty", Array.Empty<Sale>(), b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.RowsUsed().Count().Should().Be(1); // header row only
        ws.Cell(1, 1).GetString().Should().Be("Product");
    }

    // ---- F3-09: CancellationToken cancels between sheets ----

    [Fact]
    public async Task WriteAsync_CancelledToken_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = TempFile();

        var act = async () => await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b.AddColumn("P", x => x.Product))
            .AddSheet("Expenses", ExpenseData, b => b.AddColumn("C", x => x.Category))
            .WriteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- F3-10: Stream overload writes valid workbook ----

    [Fact]
    public async Task WriteAsync_ToStream_WorkbookReadable()
    {
        using var ms = new MemoryStream();

        await new MultiSheetExcelExporter(ms)
            .AddSheet("Sales", SalesData, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue))
            .WriteAsync();

        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        wb.Worksheets.Should().HaveCount(1);
        wb.Worksheets.First().Cell(2, 1).GetString().Should().Be("Widget");
    }

    // ---- F3-11: ExcelFormat applied per sheet ----

    [Fact]
    public async Task WriteAsync_ExcelFormat_AppliedPerSheet()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue, "$#,##0.00"))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 2).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        ws.Cell(2, 1).Style.NumberFormat.Format.Should().BeNullOrEmpty();
    }

    // ---- F3-12: Culture parameter accepted on path constructor ----

    [Fact]
    public async Task WriteAsync_WithCulture_DoesNotThrow()
    {
        var path = TempFile();
        var frFr = new CultureInfo("fr-FR");

        var act = async () => await new MultiSheetExcelExporter(path, frFr)
            .AddSheet("Sales", SalesData, b => b.AddColumn("P", x => x.Product))
            .WriteAsync();

        await act.Should().NotThrowAsync();
    }

    // ---- F3-13: IReportExporter.ExportAsync throws NotSupportedException ----

    [Fact]
    public async Task ExportAsync_ViaIReportExporter_ThrowsNotSupported()
    {
        var exporter = (IReportExporter)new MultiSheetExcelExporter(TempFile());
        var def = Report.Create("T").From(SalesData).AddColumn("P", x => x.Product).Build();

        var act = async () => await exporter.ExportAsync(def);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // ---- F3-14: Three or more sheets ----

    [Fact]
    public async Task WriteAsync_ThreeSheets_AllPresent()
    {
        var summaryData = new[] { new Summary("Net", 1700m) };
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b.AddColumn("P", x => x.Product))
            .AddSheet("Expenses", ExpenseData, b => b.AddColumn("C", x => x.Category))
            .AddSheet("Summary", summaryData, b => b.AddColumn("Label", x => x.Label))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.Should().HaveCount(3);
        wb.Worksheets.ElementAt(2).Name.Should().Be("Summary");
    }

    // ---- F3-15: Directory is created if missing ----

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfMissing()
    {
        var nested = Path.Combine(_tempDir, "sub", "deep", "report.xlsx");

        await new MultiSheetExcelExporter(nested)
            .AddSheet("Sales", SalesData, b => b.AddColumn("P", x => x.Product))
            .WriteAsync();

        File.Exists(nested).Should().BeTrue();
    }

    // ---- F3-16: AddColumnsFromAttributes works inside configure lambda ----

    [Fact]
    public async Task WriteAsync_AddColumnsFromAttributes_WorksInsideConfigure()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Products", ProductData(), b => b.AddColumnsFromAttributes())
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(1, 1).GetString().Should().Be("Product Name");
        ws.Cell(1, 2).GetString().Should().Be("Unit Price");
    }

    // ---- F3-17: Sheets are independent — no column bleed-over ----

    [Fact]
    public async Task WriteAsync_SheetsAreIndependent_NoColumnBleedover()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", SalesData, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue)
                .AddColumn("ExtraSales", x => x.Product))
            .AddSheet("Expenses", ExpenseData, b => b
                .AddColumn("Category", x => x.Category))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        // Sales sheet has 3 columns
        wb.Worksheets.ElementAt(0).ColumnsUsed().Count().Should().Be(3);
        // Expenses sheet has 1 column
        wb.Worksheets.ElementAt(1).ColumnsUsed().Count().Should().Be(1);
    }

    // ---- Helpers ----

    private class ProductDto
    {
        [ReportColumn("Product Name", Order = 0)]
        public string Name { get; set; } = "";

        [ReportColumn("Unit Price", Order = 1)]
        public decimal Price { get; set; }
    }

    private static ProductDto[] ProductData() =>
    [
        new ProductDto { Name = "Widget", Price = 19.99m },
        new ProductDto { Name = "Gadget", Price = 49.99m }
    ];
}
