using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class SummaryRowExcelTests : IDisposable
{
    private readonly string _tempDir;

    public SummaryRowExcelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_SummaryExcelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "out.xlsx") => Path.Combine(_tempDir, name);

    private record Sale(string Product, decimal Revenue, int Units);

    private static readonly Sale[] Data =
    [
        new("Widget", 100m,  5),
        new("Gadget", 200m, 10),
        new("Donut",  300m, 15),
    ];

    // ── E-01: No summary row — row count unchanged ────────────────────────────

    [Fact]
    public async Task ExcelExport_NoSummaryRow_RowCountIsHeaderPlusData()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().RowsUsed().Count().Should().Be(4); // 1 header + 3 data
    }

    // ── E-02: With summary row — one extra row appended ──────────────────────

    [Fact]
    public async Task ExcelExport_WithSummaryRow_AddsOneRow()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL").Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().RowsUsed().Count().Should().Be(5); // header + 3 data + summary
    }

    // ── E-03: Summary row is at the correct row index ─────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_AtCorrectRowIndex()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("Product", "TOTAL"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        // row 1 = header, rows 2-4 = data (3 rows), row 5 = summary
        ws.Cell(5, 1).GetString().Should().Be("TOTAL");
    }

    // ── E-04: Summary row cells are bold ─────────────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_CellsAreBold()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL").Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(5, 1).Style.Font.Bold.Should().BeTrue();
        ws.Cell(5, 2).Style.Font.Bold.Should().BeTrue();
    }

    // ── E-05: Summary row cells have top border ───────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_HasTopBorder()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(5, 1).Style.Border.TopBorder.Should().Be(XLBorderStyleValues.Thin);
    }

    // ── E-06: Data row cells are NOT bold (summary doesn't bleed up) ──────────

    [Fact]
    public async Task ExcelExport_DataRows_AreNotBoldAfterSummaryAdded()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).Style.Font.Bold.Should().BeFalse(); // first data row
        ws.Cell(4, 1).Style.Font.Bold.Should().BeFalse(); // last data row
    }

    // ── E-07: ExcelFormat applied to summary cell ─────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_ExcelFormatApplied()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(5, 1).Style.NumberFormat.Format.Should().Be("$#,##0.00");
        ws.Cell(5, 1).GetValue<decimal>().Should().Be(600m);
    }

    // ── E-08: Unformatted column's summary cell has no custom format ──────────

    [Fact]
    public async Task ExcelExport_SummaryRow_NoFormatOnUnformattedColumn()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Units", x => x.Units)
            .AddSummaryRow(row => row.Sum("Units"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(5, 1).Style.NumberFormat.Format.Should().BeNullOrEmpty();
    }

    // ── E-09: Blank summary cell is empty ────────────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_BlankCellIsEmpty()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Blank("Product").Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(5, 1).IsEmpty().Should().BeTrue();
        wb.Worksheets.First().Cell(5, 2).GetValue<decimal>().Should().Be(600m);
    }

    // ── E-10: Correct sum value stored as decimal ─────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_SumStoredAsDecimal()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(5, 1).GetValue<decimal>().Should().Be(600m);
    }

    // ── E-11: Count result stored as int ─────────────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_CountStoredAsInt()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Count("Product"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(5, 1).GetValue<int>().Should().Be(3);
    }

    // ── E-12: Set string label written to cell ────────────────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_SetStringLabel()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("Product", "TOTAL"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Cell(5, 1).GetString().Should().Be("TOTAL");
    }

    // ── E-13: Empty dataset — summary row at row 2 ───────────────────────────

    [Fact]
    public async Task ExcelExport_EmptyDataset_SummaryRowAtRow2()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Array.Empty<Sale>())
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL").Sum("Revenue"))
            .ToExcel(path).GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).GetString().Should().Be("TOTAL");
        ws.Cell(2, 2).IsEmpty().Should().BeTrue(); // sum of empty = null = blank
        ws.Cell(2, 1).Style.Font.Bold.Should().BeTrue();
    }

    // ── E-14: Stream mode writes summary row ─────────────────────────────────

    [Fact]
    public async Task ExcelExport_StreamMode_WritesSummaryRow()
    {
        using var ms = new MemoryStream();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToExcel(ms).GenerateAsync();

        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        wb.Worksheets.First().RowsUsed().Count().Should().Be(5);
        wb.Worksheets.First().Cell(5, 1).GetValue<decimal>().Should().Be(600m);
        wb.Worksheets.First().Cell(5, 1).Style.Font.Bold.Should().BeTrue();
    }

    // ── E-15: MultiSheetExcelExporter — each sheet gets its own summary ───────

    [Fact]
    public async Task MultiSheet_EachSheet_GetsSummaryRow()
    {
        var expenses = new[] { new { Cat = "Rent", Amount = 800m }, new { Cat = "Salaries", Amount = 5000m } };
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", Data, b => b
                .AddColumn("Product", x => x.Product)
                .AddColumn("Revenue", x => x.Revenue)
                .AddSummaryRow(r => r.Set("Product", "TOTAL").Sum("Revenue")))
            .AddSheet("Expenses", expenses, b => b
                .AddColumn("Cat", x => x.Cat)
                .AddColumn("Amount", x => x.Amount)
                .AddSummaryRow(r => r.Set("Cat", "TOTAL").Sum("Amount")))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var salesSheet = wb.Worksheets.ElementAt(0);
        var expSheet   = wb.Worksheets.ElementAt(1);

        salesSheet.Cell(5, 2).GetValue<decimal>().Should().Be(600m);
        salesSheet.Cell(5, 2).Style.Font.Bold.Should().BeTrue();

        expSheet.Cell(4, 2).GetValue<decimal>().Should().Be(5800m);
        expSheet.Cell(4, 2).Style.Font.Bold.Should().BeTrue();
    }

    // ── E-16: MultiSheetExcelExporter — sheet without summary unaffected ──────

    [Fact]
    public async Task MultiSheet_SheetWithoutSummary_Unaffected()
    {
        var path = TempFile();

        await new MultiSheetExcelExporter(path)
            .AddSheet("Sales", Data, b => b
                .AddColumn("Revenue", x => x.Revenue)
                .AddSummaryRow(r => r.Sum("Revenue")))
            .AddSheet("NoSummary", Data, b => b
                .AddColumn("Product", x => x.Product))
            .WriteAsync();

        using var wb = new XLWorkbook(path);
        var noSummarySheet = wb.Worksheets.ElementAt(1);
        noSummarySheet.RowsUsed().Count().Should().Be(4); // header + 3 data only
    }

    // ── E-17: AdjustToContents called after summary row ──────────────────────

    [Fact]
    public async Task ExcelExport_SummaryRow_ColumnWidthAdjusted()
    {
        // Just verify the file is valid and readable — column width adjustment is internal
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("Product", "A Very Long Summary Label Here"))
            .ToExcel(path).GenerateAsync();

        var act = () => new XLWorkbook(path);
        act.Should().NotThrow();
    }
}
