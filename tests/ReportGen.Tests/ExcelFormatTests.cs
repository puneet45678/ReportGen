using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class ExcelFormatTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelFormatTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_FormatTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "out.xlsx") => Path.Combine(_tempDir, name);

    // ---- Models ----

    private record SalesRow(string Product, decimal Revenue, int Units, DateTime Date, double Margin);

    private class FormattedDto
    {
        [ReportColumn("Revenue", Order = 0, Format = "$#,##0.00")]
        public decimal Revenue { get; set; }

        [ReportColumn("Order Date", Order = 1, Format = "dd/MM/yyyy")]
        public DateTime OrderDate { get; set; }

        [ReportColumn("Notes", Order = 2)]
        public string Notes { get; set; } = "";
    }

    // ---- F1-01: Currency format code is stored on the cell ----

    [Fact]
    public async Task ExcelFormat_Currency_NumberFormatApplied()
    {
        var data = new[] { new SalesRow("Widget", 1234.56m, 10, DateTime.Today, 0.25) };
        var path = TempFile();

        await Report.Create("Sales")
            .From(data)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        ws.Cell(2, 1).GetValue<decimal>().Should().Be(1234.56m);
    }

    // ---- F1-02: Date format code is stored on the cell ----

    [Fact]
    public async Task ExcelFormat_Date_NumberFormatApplied()
    {
        var data = new[] { new SalesRow("X", 0m, 0, new DateTime(2026, 3, 15), 0) };
        var path = TempFile();

        await Report.Create("Dates")
            .From(data)
            .AddColumn("Date", x => x.Date, "dd/MM/yyyy")
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        ws(wb).Cell(2, 1).Style.NumberFormat.Format.Should().Be("dd/MM/yyyy");
        ws(wb).Cell(2, 1).GetValue<DateTime>().Should().Be(new DateTime(2026, 3, 15));
    }

    // ---- F1-03: Percentage format ----

    [Fact]
    public async Task ExcelFormat_Percentage_NumberFormatApplied()
    {
        var data = new[] { new SalesRow("X", 0m, 0, DateTime.Today, 0.1234) };
        var path = TempFile();

        await Report.Create("Margins")
            .From(data)
            .AddColumn("Margin", x => x.Margin, "0.00%")
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        ws(wb).Cell(2, 1).Style.NumberFormat.Format.Should().Be("0.00%");
    }

    // ---- F1-04: Integer with thousands separator ----

    [Fact]
    public async Task ExcelFormat_IntegerWithThousands_NumberFormatApplied()
    {
        var data = new[] { new SalesRow("X", 0m, 1500000, DateTime.Today, 0) };
        var path = TempFile();

        await Report.Create("Units")
            .From(data)
            .AddColumn("Units", x => x.Units, "#,##0")
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        ws(wb).Cell(2, 1).Style.NumberFormat.Format.Should().Be("#,##0");
        ws(wb).Cell(2, 1).GetValue<int>().Should().Be(1500000);
    }

    // ---- F1-05: Null value in formatted column does not throw ----

    [Fact]
    public async Task ExcelFormat_NullValue_CellIsBlank_NoException()
    {
        var data = new[] { new { Amount = (decimal?)null, Name = "Test" } };
        var path = TempFile();

        var act = async () => await Report.Create("Nulls")
            .From(data)
            .AddColumn("Amount", x => x.Amount, "$#,##0.00")
            .AddColumn("Name", x => x.Name)
            .ToExcel(path)
            .GenerateAsync();

        await act.Should().NotThrowAsync();

        using var wb = new XLWorkbook(path);
        ws(wb).Cell(2, 1).IsEmpty().Should().BeTrue();
    }

    // ---- F1-06: Format on a string column does not throw ----

    [Fact]
    public async Task ExcelFormat_OnStringColumn_NoException()
    {
        var data = new[] { new { Name = "hello" } };
        var path = TempFile();

        var act = async () => await Report.Create("StringFmt")
            .From(data)
            .AddColumn("Name", x => x.Name, "#,##0")
            .ToExcel(path)
            .GenerateAsync();

        await act.Should().NotThrowAsync();

        using var wb = new XLWorkbook(path);
        ws(wb).Cell(2, 1).GetString().Should().Be("hello");
    }

    // ---- F1-07: No format — header cell has no custom NumberFormat ----

    [Fact]
    public async Task ExcelFormat_NoFormat_NoNumberFormatOnCell()
    {
        var data = new[] { new SalesRow("X", 99m, 1, DateTime.Today, 0) };
        var path = TempFile();

        await Report.Create("NoFmt")
            .From(data)
            .AddColumn("Revenue", x => x.Revenue)
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        // Default number format is empty or a built-in ID — not a custom format string
        ws(wb).Cell(2, 1).Style.NumberFormat.Format.Should().BeNullOrEmpty();
    }

    // ---- F1-08: Mixed columns — only formatted ones have NumberFormat ----

    [Fact]
    public async Task ExcelFormat_MixedColumns_OnlyFormattedHaveFormat()
    {
        var data = new[] { new SalesRow("Widget", 100m, 5, DateTime.Today, 0.1) };
        var path = TempFile();

        await Report.Create("Mixed")
            .From(data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .AddColumn("Units", x => x.Units)
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var sheet = ws(wb);
        sheet.Cell(2, 1).Style.NumberFormat.Format.Should().BeNullOrEmpty();
        sheet.Cell(2, 2).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        sheet.Cell(2, 3).Style.NumberFormat.Format.Should().BeNullOrEmpty();
    }

    // ---- F1-09: [ReportColumn(Format = "...")] attribute sets ExcelFormat ----

    [Fact]
    public async Task ExcelFormat_FromAttribute_AppliedToCell()
    {
        var data = new[] { new FormattedDto { Revenue = 9999.99m, OrderDate = new DateTime(2026, 6, 1), Notes = "ok" } };
        var path = TempFile();

        await Report.Create("Attr")
            .From(data)
            .AddColumnsFromAttributes()
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var sheet = ws(wb);
        sheet.Cell(2, 1).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        sheet.Cell(2, 2).Style.NumberFormat.Format.Should().Be("dd/MM/yyyy");
        sheet.Cell(2, 3).Style.NumberFormat.Format.Should().BeNullOrEmpty(); // Notes has no format
    }

    // ---- F1-10: ExcelFormat carries through ReportTemplate ----

    [Fact]
    public async Task ExcelFormat_ViaTemplate_PropagatedToDefinition()
    {
        var template = ReportTemplate<SalesRow>.Define("Sales")
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .AddColumn("Product", x => x.Product)
            .Build();

        var data = new[] { new SalesRow("Widget", 500m, 10, DateTime.Today, 0.1) };
        var path = TempFile();

        await template.From(data)
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var sheet = ws(wb);
        sheet.Cell(2, 1).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        sheet.Cell(2, 2).Style.NumberFormat.Format.Should().BeNullOrEmpty();
    }

    // ---- F1-11: ExcelFormat is stored on ColumnDefinition ----

    [Fact]
    public void ExcelFormat_StoredOnColumnDefinition()
    {
        var data = new[] { new SalesRow("X", 0m, 0, DateTime.Today, 0) };
        var def = Report.Create("Test")
            .From(data)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .AddColumn("Product", x => x.Product)
            .Build();

        def.Columns[0].ExcelFormat.Should().Be("$#,##0.00");
        def.Columns[1].ExcelFormat.Should().BeNull();
    }

    // ---- F1-12: CSV ignores ExcelFormat — raw value written ----

    [Fact]
    public async Task ExcelFormat_Csv_IsIgnored_RawValueWritten()
    {
        var data = new[] { new SalesRow("Widget", 1234.56m, 10, DateTime.Today, 0) };
        var csvPath = TempFile("out.csv");

        await Report.Create("CSV")
            .From(data)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .ToCsv(csvPath)
            .GenerateAsync();

        var csv = await File.ReadAllTextAsync(csvPath);
        // raw decimal value, not "$1,234.56"
        csv.Should().Contain("1234.56");
        csv.Should().NotContain("$");
    }

    // ---- Helper ----

    private static IXLWorksheet ws(XLWorkbook wb) => wb.Worksheets.First();
}
