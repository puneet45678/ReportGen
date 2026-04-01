namespace ReportGen.Core;

/// <summary>
/// Immutable definition of a single report column.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
/// <param name="Header">Display name for the column header.</param>
/// <param name="Accessor">Function that extracts the cell value from a row.</param>
/// <param name="Order">Zero-based column position.</param>
public sealed record ColumnDefinition<T>(
    string Header,
    Func<T, object?> Accessor,
    int Order);


