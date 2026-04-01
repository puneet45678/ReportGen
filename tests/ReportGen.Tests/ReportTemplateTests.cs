using FluentAssertions;
using ReportGen.Core;

namespace ReportGen.Tests;

public class ReportTemplateTests
{
    private record SalesRecord(string Product, decimal Revenue, int Units);

    private static readonly SalesRecord[] MarchData =
    [
        new("Widget A", 1200.50m, 100),
        new("Widget B", 3400.00m, 250)
    ];

    private static readonly SalesRecord[] AprilData =
    [
        new("Widget A", 1500.75m, 120),
        new("Widget C", 800.00m, 60)
    ];

    [Fact]
    public void Template_PreservesColumnsAcrossMultipleBindings()
    {
        var template = ReportTemplate<SalesRecord>.Define("Sales Report")
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .Build();

        var def1 = template.From(MarchData).AddColumn("Units", x => x.Units).Build();
        var def2 = template.From(AprilData).Build();

        // First binding has 3 columns (2 from template + 1 added)
        def1.Columns.Should().HaveCount(3);
        def1.Data.Should().HaveCount(2);
        def1.Data[0].Product.Should().Be("Widget A");

        // Second binding has only the 2 template columns — independent builder
        def2.Columns.Should().HaveCount(2);
        def2.Data.Should().HaveCount(2);
        def2.Data[0].Product.Should().Be("Widget A");
        def2.Data[0].Revenue.Should().Be(1500.75m);
    }

    [Fact]
    public void Template_TitleOverride_Works()
    {
        var template = ReportTemplate<SalesRecord>.Define("Sales Report")
            .AddColumn("Product", x => x.Product)
            .Build();

        var withDefault = template.From(MarchData).Build();
        var withOverride = template.From(AprilData, "Sales — April 2026").Build();

        withDefault.Title.Should().Be("Sales Report");
        withOverride.Title.Should().Be("Sales — April 2026");
    }

    [Fact]
    public void Template_IsImmutable_IndependentBuilders()
    {
        var template = ReportTemplate<SalesRecord>.Define("Immutable Test")
            .AddColumn("Product", x => x.Product)
            .Build();

        // Adding a column to one builder shouldn't affect the other
        var builder1 = template.From(MarchData);
        builder1.AddColumn("Extra", x => x.Units);

        var builder2 = template.From(AprilData);
        var def2 = builder2.Build();

        def2.Columns.Should().HaveCount(1); // only "Product" from template
    }

    [Fact]
    public void Define_WithBlankTitle_Throws()
    {
        var act = () => ReportTemplate<SalesRecord>.Define(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_WithNoColumns_Throws()
    {
        var act = () => ReportTemplate<SalesRecord>.Define("Empty").Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*column*");
    }
}
