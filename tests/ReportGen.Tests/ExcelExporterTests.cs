using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class ExcelExporterTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private record Product(string Name, decimal Price, int Stock, DateTime AddedOn);

    private static readonly Product[] SampleData =
    [
        new("Widget", 19.99m, 150, new DateTime(2026, 1, 15)),
        new("Gadget", 49.50m, 30, new DateTime(2026, 3, 1))
    ];

    private string TempFile(string name = "out.xlsx") => Path.Combine(_tempDir, name);

    // ---- Constructor validation ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_BlankPath_Throws(string path)
    {
        var act = () => new ExcelExporter(path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullPath_Throws()
    {
        var act = () => new ExcelExporter(null!);
        act.Should().Throw<ArgumentException>();
    }

    // ---- Export behaviour ----

    [Fact]
    public async Task ExportAsync_CreatesValidExcelFile()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new ExcelExporter(path).ExportAsync(def);

        File.Exists(path).Should().BeTrue();
        using var wb = new XLWorkbook(path);
        wb.Worksheets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExportAsync_SheetNameMatchesTitle()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Name.Should().Be("Products");
    }

    [Fact]
    public async Task ExportAsync_WritesHeaders_BoldInRow1()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(1, 2).GetString().Should().Be("Price");
        ws.Cell(1, 3).GetString().Should().Be("Stock");
        ws.Cell(1, 4).GetString().Should().Be("Added");
        ws.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_WritesCorrectDataValues()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).GetString().Should().Be("Widget");
        ws.Cell(2, 2).GetValue<decimal>().Should().Be(19.99m);
        ws.Cell(2, 3).GetValue<int>().Should().Be(150);
        ws.Cell(3, 1).GetString().Should().Be("Gadget");
        ws.Cell(3, 3).GetValue<int>().Should().Be(30);
    }

    [Fact]
    public async Task ExportAsync_HandlesDateTimeValues()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 4).GetValue<DateTime>().Should().Be(new DateTime(2026, 1, 15));
    }

    [Fact]
    public async Task ExportAsync_HandlesNullValues()
    {
        var data = new[] { new { Name = (string?)null, Value = 42 } };
        var def = Report.Create("Nulls")
            .From(data)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Value", x => x.Value)
            .Build();

        var path = TempFile();
        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).IsEmpty().Should().BeTrue();
        ws.Cell(2, 2).GetValue<int>().Should().Be(42);
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectoryIfMissing()
    {
        var nested = Path.Combine(_tempDir, "sub", "deep", "report.xlsx");
        var def = BuildDefinition();

        await new ExcelExporter(nested).ExportAsync(def);

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_TruncatesLongSheetName()
    {
        var longTitle = new string('X', 50); // > 31 chars
        var def = Report.Create(longTitle)
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .Build();

        var path = TempFile();
        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().Name.Length.Should().BeLessThanOrEqualTo(31);
    }

    [Fact]
    public async Task ExportAsync_SanitizesSheetNameSpecialChars()
    {
        var def = Report.Create("Q1 [Sales]: Rev*2")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .Build();

        var path = TempFile();
        await new ExcelExporter(path).ExportAsync(def);

        using var wb = new XLWorkbook(path);
        var name = wb.Worksheets.First().Name;
        name.Should().NotContainAny("[", "]", ":", "*");
    }

    [Fact]
    public async Task ExportAsync_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = TempFile();
        var def = BuildDefinition();

        var act = () => new ExcelExporter(path).ExportAsync(def, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- End-to-end via fluent API ----

    [Fact]
    public async Task FluentApi_ToExcel_WritesFile()
    {
        var path = TempFile("fluent.xlsx");

        await Report.Create("Fluent")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Price", x => x.Price)
            .ToExcel(path)
            .GenerateAsync();

        File.Exists(path).Should().BeTrue();
        using var wb = new XLWorkbook(path);
        wb.Worksheets.First().RowsUsed().Count().Should().Be(3); // header + 2 data
    }

    // ---- Multi-exporter end-to-end ----

    [Fact]
    public async Task MultiExporter_CsvAndExcel_BothWritten()
    {
        var csvPath = TempFile("multi.csv");
        var xlsxPath = TempFile("multi.xlsx");

        await Report.Create("Multi")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Price", x => x.Price)
            .ToCsv(csvPath)
            .ToExcel(xlsxPath)
            .GenerateAsync();

        File.Exists(csvPath).Should().BeTrue();
        File.Exists(xlsxPath).Should().BeTrue();
    }

    // ---- Helper ----

    private ReportDefinition<Product> BuildDefinition() =>
        Report.Create("Products")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Price", x => x.Price)
            .AddColumn("Stock", x => x.Stock)
            .AddColumn("Added", x => x.AddedOn)
            .Build();
}
