namespace ReportGen.Core;

/// <summary>
/// Entry point for the fluent report builder API.
/// </summary>
public static class Report
{
    /// <summary>
    /// Creates a new report builder with the given title.
    /// Call .From(data) next to bind data and begin adding columns.
    /// </summary>
    /// <param name="title">Report title — used in headers, sheet names, etc.</param>
    /// <returns>An <see cref="IReportBuilderSource"/> awaiting data binding.</returns>
    public static IReportBuilderSource Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Internal.ReportBuilderSource(title);
    }
}
