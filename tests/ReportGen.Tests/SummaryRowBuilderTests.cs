using FluentAssertions;
using ReportGen.Core;

namespace ReportGen.Tests;

public class SummaryRowBuilderTests
{
    private record Order(string Product, decimal Revenue, int Units, double Margin);

    private static readonly Order[] Data =
    [
        new("Widget", 100m,  5, 0.10),
        new("Gadget", 200m, 10, 0.20),
        new("Donut",  300m, 15, 0.30),
    ];

    // ── B-01: AddSummaryRow before any AddColumn throws ──────────────────────

    [Fact]
    public void AddSummaryRow_BeforeAddColumn_Throws()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddSummaryRow(row => row.Set("Product", "X"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddSummaryRow*AddColumn*");
    }

    // ── B-02: Unknown column header throws ArgumentException immediately ─────

    [Fact]
    public void Sum_UnknownColumnHeader_ThrowsArgumentException()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("DoesNotExist"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*DoesNotExist*");
    }

    [Fact]
    public void Set_UnknownColumnHeader_ThrowsArgumentException()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("NoSuchColumn", "X"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*NoSuchColumn*");
    }

    [Fact]
    public void Sum_ErrorMessage_ListsAvailableColumns()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddColumn("Units", x => x.Units)
            .AddSummaryRow(row => row.Sum("Missing"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*'Revenue'*")
            .WithMessage("*'Units'*");
    }

    // ── B-03: Null configure delegate throws ─────────────────────────────────

    [Fact]
    public void AddSummaryRow_NullConfigure_Throws()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── B-04: Build without AddSummaryRow → SummaryRow is null ──────────────

    [Fact]
    public void Build_WithoutSummaryRow_SummaryRowIsNull()
    {
        var def = Report.Create("T")
            .From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .Build();

        def.SummaryRow.Should().BeNull();
    }

    // ── B-05: Build with AddSummaryRow → SummaryRow has correct count ────────

    [Fact]
    public void Build_WithSummaryRow_SummaryRowCountMatchesColumns()
    {
        var def = Report.Create("T")
            .From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddColumn("Units",   x => x.Units)
            .AddSummaryRow(row => row.Sum("Revenue"))
            .Build();

        def.SummaryRow.Should().NotBeNull();
        def.SummaryRow!.Count.Should().Be(def.Columns.Count);
    }

    // ── B-06: Unmentioned columns default to blank (null) ────────────────────

    [Fact]
    public void Build_UnmentionedColumns_DefaultToBlank()
    {
        var def = Report.Create("T")
            .From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Set("Product", "TOTAL"))
            // Revenue is not mentioned — should be blank
            .Build();

        var revenueCell = def.SummaryRow!.First(c => c.ColumnHeader == "Revenue");
        revenueCell.Compute(def.Data).Should().BeNull();
    }

    // ── B-07: SummaryRow cells are ordered to match Columns ──────────────────

    [Fact]
    public void Build_SummaryRowCells_OrderMatchesColumns()
    {
        var def = Report.Create("T")
            .From(Data)
            .AddColumn("Product", x => x.Product)
            .AddColumn("Revenue", x => x.Revenue)
            .AddColumn("Units",   x => x.Units)
            .AddSummaryRow(row => row
                .Set("Product", "TOTAL")
                .Sum("Revenue")
                .Sum("Units"))
            .Build();

        for (var i = 0; i < def.Columns.Count; i++)
            def.SummaryRow![i].ColumnHeader.Should().Be(def.Columns[i].Header);
    }

    // ── B-08: Compute with null delegate throws ArgumentNullException ─────────

    [Fact]
    public void Compute_NullDelegate_ThrowsArgumentNullException()
    {
        var act = () => Report.Create("T")
            .From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Compute("Revenue", null!));

        act.Should().Throw<ArgumentNullException>();
    }

    // ── B-09: Sum all-null column returns null ────────────────────────────────

    [Fact]
    public void Sum_AllNullColumn_ReturnsNull()
    {
        var nullData = new[] { new { Val = (int?)null }, new { Val = (int?)null } };
        var def = Report.Create("T")
            .From(nullData)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Sum("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    // ── B-10: Average all-null column returns null ────────────────────────────

    [Fact]
    public void Average_AllNullColumn_ReturnsNull()
    {
        var nullData = new[] { new { Val = (decimal?)null } };
        var def = Report.Create("T")
            .From(nullData)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Average("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    // ── B-11: Count all-null column returns 0 (not null) ─────────────────────

    [Fact]
    public void Count_AllNullColumn_ReturnsZero()
    {
        var nullData = new[] { new { Val = (int?)null }, new { Val = (int?)null } };
        var def = Report.Create("T")
            .From(nullData)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Count("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(0);
    }

    // ── B-12: Sum skips null values ───────────────────────────────────────────

    [Fact]
    public void Sum_PartialNullColumn_SkipsNulls()
    {
        var mixed = new[] { new { Val = (decimal?)10m }, new { Val = (decimal?)null }, new { Val = (decimal?)20m } };
        var def = Report.Create("T")
            .From(mixed)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Sum("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(30m);
    }

    // ── B-13: Average excludes null from denominator ──────────────────────────

    [Fact]
    public void Average_PartialNullColumn_ExcludesNullFromDenominator()
    {
        // 10 + 20 = 30 / 2 non-null = 15 (not 30/3 = 10)
        var mixed = new[] { new { Val = (decimal?)10m }, new { Val = (decimal?)null }, new { Val = (decimal?)20m } };
        var def = Report.Create("T")
            .From(mixed)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Average("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(15m);
    }

    // ── B-14: Count counts non-null values ───────────────────────────────────

    [Fact]
    public void Count_PartialNullColumn_CountsNonNullOnly()
    {
        var mixed = new[] { new { Val = (int?)1 }, new { Val = (int?)null }, new { Val = (int?)3 } };
        var def = Report.Create("T")
            .From(mixed)
            .AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Count("Val"))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(2);
    }

    // ── B-15: Min / Max on all-null return null ───────────────────────────────

    [Fact]
    public void Min_AllNull_ReturnsNull()
    {
        var nullData = new[] { new { Val = (int?)null } };
        var def = Report.Create("T").From(nullData).AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Min("Val")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    [Fact]
    public void Max_AllNull_ReturnsNull()
    {
        var nullData = new[] { new { Val = (int?)null } };
        var def = Report.Create("T").From(nullData).AddColumn("Val", x => x.Val)
            .AddSummaryRow(row => row.Max("Val")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    // ── B-16: Correct numeric aggregation results ─────────────────────────────

    [Fact]
    public void Sum_IntColumn_ReturnsCorrectSum()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Units", x => x.Units)
            .AddSummaryRow(row => row.Sum("Units")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().Be(30m); // 5+10+15
    }

    [Fact]
    public void Average_DecimalColumn_ReturnsCorrectAverage()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Average("Revenue")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().Be(200m); // (100+200+300)/3
    }

    [Fact]
    public void Min_DecimalColumn_ReturnsMinimum()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Min("Revenue")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().Be(100m);
    }

    [Fact]
    public void Max_DecimalColumn_ReturnsMaximum()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Max("Revenue")).Build();
        def.SummaryRow![0].Compute(def.Data).Should().Be(300m);
    }

    // ── B-17: Non-numeric column with Sum throws at compute time ─────────────

    [Fact]
    public void Sum_NonNumericColumn_ThrowsInvalidCastAtComputeTime()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Sum("Product")).Build();

        var act = () => def.SummaryRow![0].Compute(def.Data);
        act.Should().Throw<InvalidCastException>()
            .WithMessage("*Product*");
    }

    // ── B-18: Compute custom lambda receives correct data ────────────────────

    [Fact]
    public void Compute_CustomLambda_ReceivesFullDataList()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Compute("Revenue", rows => rows.Count))
            .Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(3);
    }

    // ── B-19: AddColumn after AddSummaryRow causes Build() to throw ──────────

    [Fact]
    public void Build_AddColumnAfterAddSummaryRow_Throws()
    {
        var act = () =>
        {
            var builder = Report.Create("T")
                .From(Data)
                .AddColumn("Revenue", x => x.Revenue)
                .AddSummaryRow(row => row.Sum("Revenue"));

            builder.AddColumn("Units", x => x.Units); // added after summary row
            builder.Build();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddColumn*AddSummaryRow*");
    }

    // ── B-20: Set stores static value correctly ───────────────────────────────

    [Fact]
    public void Set_StaticStringValue_StoredAndReturned()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("Product", "TOTAL")).Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be("TOTAL");
    }

    [Fact]
    public void Set_NullValue_ReturnsNull()
    {
        var def = Report.Create("T").From(Data)
            .AddColumn("Product", x => x.Product)
            .AddSummaryRow(row => row.Set("Product", null)).Build();

        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    // ── B-21: Empty dataset ───────────────────────────────────────────────────

    [Fact]
    public void Sum_EmptyDataset_ReturnsNull()
    {
        var def = Report.Create("T").From(Array.Empty<Order>())
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Sum("Revenue")).Build();

        def.SummaryRow![0].Compute(def.Data).Should().BeNull();
    }

    [Fact]
    public void Count_EmptyDataset_ReturnsZero()
    {
        var def = Report.Create("T").From(Array.Empty<Order>())
            .AddColumn("Revenue", x => x.Revenue)
            .AddSummaryRow(row => row.Count("Revenue")).Build();

        def.SummaryRow![0].Compute(def.Data).Should().Be(0);
    }
}
