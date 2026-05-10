namespace ReportGen.Core;

/// <summary>
/// Immutable definition of a single report column.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
/// <param name="Header">Display name for the column header.</param>
/// <param name="Accessor">Function that extracts the cell value from a row.</param>
/// <param name="Order">Zero-based column position.</param>
/// <param name="ExcelFormat">
/// Optional Excel number format string applied to data cells (e.g. <c>"$#,##0.00"</c>,
/// <c>"dd/MM/yyyy"</c>, <c>"0.00%"</c>, <c>"#,##0"</c>).
/// Ignored by CSV exporters. <see langword="null"/> means no format is applied.
/// </param>
public sealed record ColumnDefinition<T>(
    string Header,
    Func<T, object?> Accessor,
    int Order,
    string? ExcelFormat = null);


