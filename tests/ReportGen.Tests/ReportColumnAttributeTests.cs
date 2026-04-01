using FluentAssertions;
using ReportGen.Core;

namespace ReportGen.Tests;

public class ReportColumnAttributeTests
{
    // ----- Test DTOs -----

    private class EmployeeDto
    {
        [ReportColumn("Employee Name", Order = 0)]
        public string Name { get; set; } = "";

        [ReportColumn("Email Address", Order = 1)]
        public string Email { get; set; } = "";

        public string InternalId { get; set; } = "";   // no attribute — excluded
    }

    private class DefaultHeaderDto
    {
        [ReportColumn]
        public string City { get; set; } = "";

        [ReportColumn]
        public int Age { get; set; }
    }

    private class NoAttributesDto
    {
        public string Name { get; set; } = "";
    }

    private class MixedOrderDto
    {
        [ReportColumn("Third", Order = 2)]
        public string C { get; set; } = "";

        [ReportColumn("First", Order = 0)]
        public string A { get; set; } = "";

        [ReportColumn("Second", Order = 1)]
        public string B { get; set; } = "";
    }

    // ----- Tests: Builder -----

    [Fact]
    public void AddColumnsFromAttributes_DiscoversDecoratedProperties()
    {
        var data = new[] { new EmployeeDto { Name = "Ava", Email = "ava@co.com", InternalId = "X1" } };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .Build();

        definition.Columns.Should().HaveCount(2);
        definition.Columns[0].Header.Should().Be("Employee Name");
        definition.Columns[1].Header.Should().Be("Email Address");
    }

    [Fact]
    public void AddColumnsFromAttributes_ExcludesUndecoratedProperties()
    {
        var data = new[] { new EmployeeDto { Name = "Ava", Email = "ava@co.com", InternalId = "secret" } };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .Build();

        definition.Columns.Select(c => c.Header)
            .Should().NotContain("InternalId");
    }

    [Fact]
    public void AddColumnsFromAttributes_UsesPropertyName_WhenNoHeaderSpecified()
    {
        var data = new[] { new DefaultHeaderDto { City = "Delhi", Age = 25 } };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .Build();

        definition.Columns.Select(c => c.Header)
            .Should().Contain("City")
            .And.Contain("Age");
    }

    [Fact]
    public void AddColumnsFromAttributes_RespectsOrderAttribute()
    {
        var data = new[] { new MixedOrderDto { A = "a", B = "b", C = "c" } };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .Build();

        definition.Columns[0].Header.Should().Be("First");
        definition.Columns[1].Header.Should().Be("Second");
        definition.Columns[2].Header.Should().Be("Third");
    }

    [Fact]
    public void AddColumnsFromAttributes_AccessorsExtractCorrectValues()
    {
        var row = new EmployeeDto { Name = "Ava", Email = "ava@co.com", InternalId = "X1" };
        var data = new[] { row };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .Build();

        definition.Columns[0].Accessor(row).Should().Be("Ava");
        definition.Columns[1].Accessor(row).Should().Be("ava@co.com");
    }

    [Fact]
    public void AddColumnsFromAttributes_ThrowsWhenNoAttributesFound()
    {
        var data = new[] { new NoAttributesDto { Name = "Ava" } };

        var act = () => Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NoAttributesDto*")
            .WithMessage("*[ReportColumn]*");
    }

    // ----- Tests: Mixed (attributes + manual columns) -----

    [Fact]
    public void MixedApproach_AttributesPlusManualColumns()
    {
        var data = new[]
        {
            new EmployeeDto { Name = "Ava", Email = "ava@co.com", InternalId = "X1" }
        };

        var definition = Report.Create("Test")
            .From(data)
            .AddColumnsFromAttributes()
            .AddColumn("ID (manual)", x => x.InternalId)
            .Build();

        definition.Columns.Should().HaveCount(3);
        definition.Columns[0].Header.Should().Be("Employee Name");
        definition.Columns[1].Header.Should().Be("Email Address");
        definition.Columns[2].Header.Should().Be("ID (manual)");
    }

    // ----- Tests: Template builder -----

    [Fact]
    public void Template_AddColumnsFromAttributes_Works()
    {
        var template = ReportTemplate<EmployeeDto>.Define("Employee Report")
            .AddColumnsFromAttributes()
            .Build();

        template.Columns.Should().HaveCount(2);
        template.Columns[0].Header.Should().Be("Employee Name");
        template.Columns[1].Header.Should().Be("Email Address");
    }

    [Fact]
    public void Template_MixedApproach_Works()
    {
        var data = new[]
        {
            new EmployeeDto { Name = "Ava", Email = "ava@co.com", InternalId = "X1" }
        };

        var template = ReportTemplate<EmployeeDto>.Define("Employee Report")
            .AddColumnsFromAttributes()
            .AddColumn("Secret ID", x => x.InternalId)
            .Build();

        var definition = template.From(data).Build();

        definition.Columns.Should().HaveCount(3);
        definition.Columns[2].Header.Should().Be("Secret ID");
    }
}
