namespace ReportGen.Core;

/// <summary>
/// Fluent builder for configuring the summary (footer) row of a report.
/// Obtained via <see cref="IReportBuilder{T}.AddSummaryRow"/>.
/// <para>
/// Columns not explicitly configured default to a blank cell.
/// Column headers are case-sensitive and must match the values passed to <c>AddColumn</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public interface ISummaryRowBuilder<T>
{
    /// <summary>
    /// Writes a static value into this column's summary cell.
    /// </summary>
    /// <param name="columnHeader">Header of the target column.</param>
    /// <param name="value">
    /// The static value to display — string, number, <see langword="null"/> (blank), or any object.
    /// </param>
    ISummaryRowBuilder<T> Set(string columnHeader, object? value);

    /// <summary>
    /// Sums all non-null numeric values in this column.
    /// Returns <see langword="null"/> (blank cell) if all values are null.
    /// <para>
    /// Uses <see cref="Convert.ToDecimal(object)"/> internally — suitable for
    /// <c>int</c>, <c>long</c>, <c>double</c>, <c>float</c>, <c>decimal</c>.
    /// Throws <see cref="InvalidCastException"/> at generation time for non-numeric columns.
    /// </para>
    /// </summary>
    ISummaryRowBuilder<T> Sum(string columnHeader);

    /// <summary>
    /// Computes the mean of all non-null numeric values in this column.
    /// Null values are excluded from both the numerator and the denominator.
    /// Returns <see langword="null"/> (blank cell) if all values are null.
    /// </summary>
    ISummaryRowBuilder<T> Average(string columnHeader);

    /// <summary>
    /// Counts non-null values returned by this column's accessor.
    /// Always returns an <c>int</c> (returns <c>0</c>, not null, when all values are null).
    /// </summary>
    ISummaryRowBuilder<T> Count(string columnHeader);

    /// <summary>
    /// Returns the minimum non-null numeric value in this column.
    /// Returns <see langword="null"/> (blank cell) if all values are null.
    /// </summary>
    ISummaryRowBuilder<T> Min(string columnHeader);

    /// <summary>
    /// Returns the maximum non-null numeric value in this column.
    /// Returns <see langword="null"/> (blank cell) if all values are null.
    /// </summary>
    ISummaryRowBuilder<T> Max(string columnHeader);

    /// <summary>
    /// Explicitly marks this column's summary cell as blank.
    /// This is also the default for any column not configured in the builder.
    /// </summary>
    ISummaryRowBuilder<T> Blank(string columnHeader);

    /// <summary>
    /// Supplies a fully custom aggregation function for this column.
    /// The function receives the full materialized data list and returns the cell value.
    /// </summary>
    /// <param name="columnHeader">Header of the target column.</param>
    /// <param name="compute">Aggregation function. Return <see langword="null"/> for a blank cell.</param>
    ISummaryRowBuilder<T> Compute(string columnHeader, Func<IReadOnlyList<T>, object?> compute);
}
