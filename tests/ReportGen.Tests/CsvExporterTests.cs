using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class CsvExporterTests : IDisposable
{
    private readonly string _tempDir;

    public CsvExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private record Employee(string Name, string Email, int Score);

    private static readonly Employee[] SampleData =
    [
        new("Ava", "ava@co.com", 92),
        new("Noah", "noah@co.com", 88)
    ];

    private string TempFile(string name = "out.csv") => Path.Combine(_tempDir, name);

    // ---- Constructor validation ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_BlankPath_Throws(string path)
    {
        var act = () => new CsvExporter(path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullPath_Throws()
    {
        var act = () => new CsvExporter(null!);
        act.Should().Throw<ArgumentException>();
    }

    // ---- Export behaviour ----

    [Fact]
    public async Task ExportAsync_WritesHeaderRow()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new CsvExporter(path).ExportAsync(def);

        var lines = await File.ReadAllLinesAsync(path);
        lines[0].Should().Be("Name,Email,Score");
    }

    [Fact]
    public async Task ExportAsync_WritesCorrectDataRows()
    {
        var path = TempFile();
        var def = BuildDefinition();

        await new CsvExporter(path).ExportAsync(def);

        var lines = await File.ReadAllLinesAsync(path);
        lines.Should().HaveCount(3); // 1 header + 2 data
        lines[1].Should().Be("Ava,ava@co.com,92");
        lines[2].Should().Be("Noah,noah@co.com,88");
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectoryIfMissing()
    {
        var nested = Path.Combine(_tempDir, "sub", "deep", "report.csv");
        var def = BuildDefinition();

        await new CsvExporter(nested).ExportAsync(def);

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_OverwritesExistingFile()
    {
        var path = TempFile();
        await File.WriteAllTextAsync(path, "old content");

        var def = BuildDefinition();
        await new CsvExporter(path).ExportAsync(def);

        var content = await File.ReadAllTextAsync(path);
        content.Should().NotContain("old content");
        content.Should().Contain("Ava");
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
        await new CsvExporter(path).ExportAsync(def);

        var lines = await File.ReadAllLinesAsync(path);
        lines[1].Should().Be(",42");
    }

    [Fact]
    public async Task ExportAsync_RespectsColumnOrder()
    {
        var data = new[] { new { A = "a", B = "b", C = "c" } };
        var def = Report.Create("Order")
            .From(data)
            .AddColumn("Third", x => x.C)
            .AddColumn("First", x => x.A)
            .AddColumn("Second", x => x.B)
            .Build();

        var path = TempFile();
        await new CsvExporter(path).ExportAsync(def);

        var lines = await File.ReadAllLinesAsync(path);
        // Columns appear in the order they were added (Order 0, 1, 2)
        lines[0].Should().Be("Third,First,Second");
    }

    [Fact]
    public async Task ExportAsync_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = TempFile();
        var def = BuildDefinition();

        var act = () => new CsvExporter(path).ExportAsync(def, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- End-to-end via fluent API ----

    [Fact]
    public async Task FluentApi_ToCsv_WritesFile()
    {
        var path = TempFile("fluent.csv");

        await Report.Create("Fluent")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .ToCsv(path)
            .GenerateAsync();

        File.Exists(path).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(path);
        lines.Should().HaveCountGreaterThan(1);
    }

    // ---- Helper ----

    private ReportDefinition<Employee> BuildDefinition() =>
        Report.Create("Test Report")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Email", x => x.Email)
            .AddColumn("Score", x => x.Score)
            .Build();
}
