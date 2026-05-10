namespace ReportGen.Core;

/// <summary>
/// Typed fluent builder for constructing and executing a report.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public interface IReportBuilder<T>
{
    /// <summary>
    /// Adds a column definition to the report.
    /// </summary>
    /// <param name="header">Column header text.</param>
    /// <param name="accessor">Function that extracts the cell value from a row of type T.</param>
    IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor);

    /// <summary>
    /// Adds a column definition with an optional Excel number format string.
    /// </summary>
    /// <param name="header">Column header text.</param>
    /// <param name="accessor">Function that extracts the cell value from a row of type T.</param>
    /// <param name="excelFormat">
    /// Excel number format string applied to data cells (e.g. <c>"$#,##0.00"</c>,
    /// <c>"dd/MM/yyyy"</c>, <c>"0.00%"</c>). Ignored by CSV exporters.
    /// </param>
    /// <returns>The builder, for fluent chaining.</returns>
    IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor, string? excelFormat)
        => AddColumn(header, accessor);

    /// <summary>
    /// Registers an exporter to be executed during generation.
    /// This is the primitive extension point — .ToCsv() and .ToExcel() are sugar on top of this.
    /// </summary>
    IReportBuilder<T> AddExporter(IReportExporter exporter);

    /// <summary>
    /// Builds the immutable <see cref="ReportDefinition{T}"/> without executing exports.
    /// Useful for testing or inspecting the definition before export.
    /// </summary>
    ReportDefinition<T> Build();

    /// <summary>
    /// Builds the report definition and executes all registered exporters sequentially.
    /// </summary>
    Task GenerateAsync(CancellationToken cancellationToken = default);
}
