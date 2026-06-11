namespace ReportGen.Core;

/// <summary>
/// Defines how a single cell in the summary row is computed from the report data.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
/// <param name="ColumnHeader">
/// The header of the target column, matching the value passed to <c>AddColumn</c>.
/// </param>
/// <param name="Compute">
/// A function that receives the full materialized data list and returns the cell value.
/// A return value of <see langword="null"/> renders a blank cell.
/// </param>
public sealed record SummaryCellDefinition<T>(
    string ColumnHeader,
    Func<IReadOnlyList<T>, object?> Compute);
