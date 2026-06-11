using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class SummaryRowCsvTests : IDisposable
{
    private readonly string _tempDir;

    public SummaryRowCsvTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_SummaryCsvTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "out.csv") => Path.Combine(_tempDir, name);

    private record Sale(string Product, decimal Revenue, int Units);

    private static readonly Sale[] Data =
    [
        new("Widget", 100m,  5),
        new("Gadget", 200m, 10),
        new("Donut",  300m, 15),
    ];

    // ── C-01: No summary row — line count unchanged ───────────────────────────

    [Fact]
    public async Task CsvExport_NoSummaryRow_LineCountIsHeaderPlusData()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Should().HaveCount(4); // 1 header + 3 data
    }

    // ── C-02: With summary row — one extra line appended ─────────────────────

    [Fact]
    public async Task CsvExport_WithSummaryRow_AppendsOneLine()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL").Sum("Revenue"))
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Should().HaveCount(5); // 1 header + 3 data + 1 summary
    }

    // ── C-03: Summary row is the last line ────────────────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_IsLastLine()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL").Sum("Revenue"))
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Last().Should().Contain("TOTAL");
        lines.Last().Should().Contain("600"); // 100+200+300
    }

    // ── C-04: Sum value is correct ────────────────────────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_SumIsCorrect()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Units", x => x.Units)
            .AddSummaryRow(row => row.Sum("Units"))
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Last().Should().Contain("30"); // 5+10+15
    }

    // ── C-05: Average value is correct ───────────────────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_AverageIsCorrect()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Average("Revenue"))
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Last().Should().Contain("200"); // (100+200+300)/3
    }

    // ── C-06: Count value is correct ──────────────────────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_CountIsCorrect()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Count("Product"))
            .ToCsv(path).GenerateAsync();

        var lines = await File.ReadAllLinesAsync(path);
        lines.Last().Should().Contain("3");
    }

    // ── C-07: Min and Max values are correct ──────────────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_MinAndMaxAreCorrect()
    {
        var minPath = TempFile("min.csv");
        var maxPath = TempFile("max.csv");

        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Min("Revenue"))
            .ToCsv(minPath).GenerateAsync();

        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Max("Revenue"))
            .ToCsv(maxPath).GenerateAsync();

        (await File.ReadAllLinesAsync(minPath)).Last().Should().Contain("100");
        (await File.ReadAllLinesAsync(maxPath)).Last().Should().Contain("300");
    }

    // ── C-08: Blank summary cell writes empty field ───────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_BlankCellWritesEmptyField()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Blank("Product").Sum("Revenue"))
            .ToCsv(path).GenerateAsync();

        var lastLine = (await File.ReadAllLinesAsync(path)).Last();
        lastLine.Should().StartWith(","); // blank product = empty first field
        lastLine.Should().Contain("600");
    }

    // ── C-09: ExcelFormat on column is ignored for CSV ────────────────────────

    [Fact]
    public async Task CsvExport_SummaryRow_ExcelFormatIgnored()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue, "$#,##0.00")
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToCsv(path).GenerateAsync();

        var lastLine = (await File.ReadAllLinesAsync(path)).Last();
        lastLine.Should().Contain("600");
        lastLine.Should().NotContain("$");
    }

    // ── C-10: Stream mode writes summary row ─────────────────────────────────

    [Fact]
    public async Task CsvExport_StreamMode_WritesSummaryRow()
    {
        using var ms = new MemoryStream();
        await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToCsv(ms).GenerateAsync();

        ms.Position = 0;
        var lines = new StreamReader(ms).ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(5); // header + 3 data + summary
        lines.Last().Should().Contain("600");
    }

    // ── C-11: Cancellation before summary row write ───────────────────────────

    [Fact]
    public async Task CsvExport_CancellationBeforeSummaryRow_Throws()
    {
        // Use a large dataset so the data loop completes but we can cancel
        // before the summary row. Here we just cancel upfront and check it propagates.
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = TempFile();
        var act = async () => await Report.Create("Sales").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .ToCsv(path).GenerateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── C-12: All aggregators in one report ───────────────────────────────────

    [Fact]
    public async Task CsvExport_AllAggregators_WriteCorrectly()
    {
        var path = TempFile();
        await Report.Create("Sales").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddColumn("Units",   x => x.Units)
            .AddSummaryRow(row => row
                .Set("Product", "TOTAL")
                .Sum("Revenue")
                .Sum("Units"))
            .ToCsv(path).GenerateAsync();

        var lastLine = (await File.ReadAllLinesAsync(path)).Last();
        lastLine.Should().Contain("TOTAL");
        lastLine.Should().Contain("600");
        lastLine.Should().Contain("30");
    }
}
