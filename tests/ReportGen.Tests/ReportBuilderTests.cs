using FluentAssertions;
using ReportGen.Core;

namespace ReportGen.Tests;

public class ReportBuilderTests
{
    private record TestRow(string Name, string Email, int Score);

    private static readonly TestRow[] SampleData =
    [
        new("Ava", "ava@company.com", 92),
        new("Noah", "noah@company.com", 88)
    ];

    [Fact]
    public void Build_WithColumns_ProducesCorrectDefinition()
    {
        var definition = Report.Create("Test Report")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .AddColumn("Email", x => x.Email)
            .AddColumn("Score", x => x.Score)
            .Build();

        definition.Title.Should().Be("Test Report");
        definition.Columns.Should().HaveCount(3);
        definition.Data.Should().HaveCount(2);
        definition.Columns[0].Header.Should().Be("Name");
        definition.Columns[1].Header.Should().Be("Email");
        definition.Columns[2].Header.Should().Be("Score");
    }

    [Fact]
    public void Build_WithNoColumns_Throws()
    {
        var builder = Report.Create("Empty")
            .From(SampleData);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*column*");
    }

    [Fact]
    public void Create_WithBlankTitle_Throws()
    {
        var act = () => Report.Create("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_MaterializesData_AsReadOnlyList()
    {
        var definition = Report.Create("Materialization Test")
            .From(SampleData)
            .AddColumn("Name", x => x.Name)
            .Build();

        definition.Data.Should().BeAssignableTo<IReadOnlyList<TestRow>>();
        definition.Data.Should().HaveCount(2);
    }

    [Fact]
    public void ColumnAccessor_ExtractsCorrectValues()
    {
        var definition = Report.Create("Accessor Test")
            .From(SampleData)
            .AddColumn("Score", x => x.Score)
            .Build();

        var accessor = definition.Columns[0].Accessor;
        accessor(SampleData[0]).Should().Be(92);
        accessor(SampleData[1]).Should().Be(88);
    }

    [Fact]
    public async Task GenerateAsync_WithNoExporters_Throws()
    {
        var builder = Report.Create("No Exporters")
            .From(SampleData)
            .AddColumn("Name", x => x.Name);

        var act = () => builder.GenerateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exporter*");
    }
}
