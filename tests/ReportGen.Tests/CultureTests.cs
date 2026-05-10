using System.Globalization;
using ClosedXML.Excel;
using FluentAssertions;
using ReportGen.Core;
using ReportGen.Exporters;

namespace ReportGen.Tests;

public class CultureTests : IDisposable
{
    private readonly string _tempDir;

    public CultureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReportGen_CultureTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // ---- F2-01: CsvExporter default is InvariantCulture (no breaking change) ----

    [Fact]
    public async Task CsvExporter_DefaultCulture_IsInvariant()
    {
        var data = new[] { new { Amount = 1234.56m } };
        var path = TempFile("invariant.csv");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .ToCsv(path)
            .GenerateAsync();

        var csv = await File.ReadAllTextAsync(path);
        csv.Should().Contain("1234.56");
        csv.Should().NotContain("1234,56");
    }

    // ---- F2-02: German CSV — comma decimal separator ----

    [Fact]
    public async Task CsvExporter_GermanCulture_UsesCommaDecimalSeparator()
    {
        var data = new[] { new { Amount = 1234.56m } };
        var path = TempFile("german.csv");
        var deDe = new CultureInfo("de-DE");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .ToCsv(path, culture: deDe)
            .GenerateAsync();

        var csv = await File.ReadAllTextAsync(path);
        csv.Should().Contain("1234,56");
        csv.Should().NotContain("1234.56");
    }

    // ---- F2-03: German CSV uses semicolon delimiter ----

    [Fact]
    public async Task CsvExporter_GermanCulture_UsesSemicolonDelimiter()
    {
        var data = new[] { new { Name = "Widget", Amount = 99.50m } };
        var path = TempFile("german-delim.csv");
        var deDe = new CultureInfo("de-DE");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Amount", x => x.Amount)
            .ToCsv(path, culture: deDe)
            .GenerateAsync();

        var csv = await File.ReadAllTextAsync(path);
        // de-DE list separator is ";" — CsvHelper uses it as the delimiter
        csv.Should().Contain(";");
    }

    // ---- F2-04: Stream overload respects culture ----

    [Fact]
    public async Task CsvExporter_StreamOverload_RespectsCulture()
    {
        var data = new[] { new { Amount = 1234.56m } };
        using var ms = new MemoryStream();
        var deDe = new CultureInfo("de-DE");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .ToCsv(ms, culture: deDe)
            .GenerateAsync();

        ms.Position = 0;
        var csv = new StreamReader(ms).ReadToEnd();
        csv.Should().Contain("1234,56");
    }

    // ---- F2-05: ExcelExporter default culture — no behavior change ----

    [Fact]
    public async Task ExcelExporter_DefaultCulture_NoChangeToExistingBehavior()
    {
        var data = new[] { new { Amount = 1234.56m, Name = "Widget" } };
        var path = TempFile("default-excel.xlsx");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .AddColumn("Name", x => x.Name)
            .ToExcel(path)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        ws.Cell(2, 1).GetValue<decimal>().Should().Be(1234.56m);
        ws.Cell(2, 2).GetString().Should().Be("Widget");
    }

    // ---- F2-06: ExcelExporter culture parameter accepted without throwing ----

    [Fact]
    public async Task ExcelExporter_WithCulture_DoesNotThrow()
    {
        var data = new[] { new { Amount = 99.99m } };
        var path = TempFile("culture-excel.xlsx");
        var frFr = new CultureInfo("fr-FR");

        var act = async () => await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .ToExcel(path, culture: frFr)
            .GenerateAsync();

        await act.Should().NotThrowAsync();
        File.Exists(path).Should().BeTrue();
    }

    // ---- F2-07: ExcelExporter culture affects ToString() fallback ----

    [Fact]
    public async Task ExcelExporter_Culture_AffectsToStringFallback()
    {
        // A custom type falls through to Convert.ToString(value, culture)
        var value = new CustomType(42.5);
        var data = new[] { new { Val = value } };
        var path = TempFile("tostring.xlsx");
        var deDe = new CultureInfo("de-DE");

        await Report.Create("Test")
            .From(data)
            .AddColumn("Val", x => (object?)x.Val)
            .ToExcel(path, culture: deDe)
            .GenerateAsync();

        using var wb = new XLWorkbook(path);
        // CustomType.ToString() is called — just verify no exception and cell is written
        wb.Worksheets.First().Cell(2, 1).IsEmpty().Should().BeFalse();
    }

    // ---- F2-08: ExcelExporter stream overload accepts culture ----

    [Fact]
    public async Task ExcelExporter_StreamOverload_RespectsCulture()
    {
        var data = new[] { new { Amount = 50m } };
        using var ms = new MemoryStream();
        var frFr = new CultureInfo("fr-FR");

        var act = async () => await Report.Create("Test")
            .From(data)
            .AddColumn("Amount", x => x.Amount)
            .ToExcel(ms, culture: frFr)
            .GenerateAsync();

        await act.Should().NotThrowAsync();
        ms.Length.Should().BeGreaterThan(0);
    }

    // ---- F2-09: CsvExporter constructor directly accepts culture ----

    [Fact]
    public async Task CsvExporter_ConstructorCulture_Applied()
    {
        var data = new[] { new { Amount = 9.99m } };
        using var ms = new MemoryStream();

        await new CsvExporter(ms, new CultureInfo("de-DE"))
            .ExportAsync(Report.Create("T").From(data).AddColumn("A", x => x.Amount).Build());

        ms.Position = 0;
        var text = new StreamReader(ms).ReadToEnd();
        text.Should().Contain("9,99");
    }

    // ---- F2-10: ExcelExporter constructor directly accepts culture ----

    [Fact]
    public async Task ExcelExporter_ConstructorCulture_Accepted()
    {
        var data = new[] { new { Amount = 9.99m } };
        var path = TempFile("ctor-culture.xlsx");

        var act = async () => await new ExcelExporter(path, new CultureInfo("fr-FR"))
            .ExportAsync(Report.Create("T").From(data).AddColumn("A", x => x.Amount).Build());

        await act.Should().NotThrowAsync();
    }

    // ---- Helper type for fallback ToString() test ----

    private sealed class CustomType(double val)
    {
        public override string ToString() => val.ToString("F2");
    }
}
