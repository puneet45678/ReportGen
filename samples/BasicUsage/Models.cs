using ReportGen.Core;

namespace BasicUsage;

// -----------------------------------------------------------------
// Core domain models used across all demos
// -----------------------------------------------------------------

public record Employee(
    int Id,
    string Name,
    string Department,
    string Email,
    decimal Salary,
    int YearsOfExperience,
    DateTime JoinedOn,
    bool IsActive);

public record SalesOrder(
    int OrderId,
    string CustomerName,
    string Region,
    string Product,
    int Quantity,
    decimal UnitPrice,
    DateOnly OrderDate,
    bool IsShipped);

// Used in the attribute-discovery demo — columns declared via attributes
public class ProductSnapshot
{
    [ReportColumn("Product ID", Order = 1)]
    public int Id { get; init; }

    [ReportColumn("Product Name", Order = 2)]
    public string Name { get; init; } = "";

    [ReportColumn("Category", Order = 3)]
    public string Category { get; init; } = "";

    [ReportColumn("Unit Price", Order = 4)]
    public decimal UnitPrice { get; init; }

    [ReportColumn("Stock", Order = 5)]
    public int StockLevel { get; init; }

    [ReportColumn("Last Restocked", Order = 6)]
    public DateOnly LastRestocked { get; init; }

    // This property has no attribute — intentionally ignored by attribute discovery
    public string InternalCode { get; init; } = "";
}
