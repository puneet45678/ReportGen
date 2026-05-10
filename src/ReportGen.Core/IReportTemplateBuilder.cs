namespace ReportGen.Core;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="ReportTemplate{T}"/>.
/// </summary>
/// <typeparam name="T">The row data type this template is designed for.</typeparam>
public interface IReportTemplateBuilder<T>
{
    /// <summary>Adds a column definition to the template.</summary>
    IReportTemplateBuilder<T> AddColumn(string header, Func<T, object?> accessor);

    /// <summary>
    /// Adds a column definition with an optional Excel number format string.
    /// </summary>
    /// <param name="header">Column header text.</param>
    /// <param name="accessor">Function that extracts the cell value from a row of type T.</param>
    /// <param name="excelFormat">
    /// Excel number format string applied to data cells (e.g. <c>"$#,##0.00"</c>,
    /// <c>"dd/MM/yyyy"</c>, <c>"0.00%"</c>). Ignored by CSV exporters.
    /// </param>
    IReportTemplateBuilder<T> AddColumn(string header, Func<T, object?> accessor, string? excelFormat)
        => AddColumn(header, accessor);

    /// <summary>Builds the immutable, reusable template.</summary>
    ReportTemplate<T> Build();
}
