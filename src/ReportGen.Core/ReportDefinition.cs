namespace ReportGen.Core;

/// <summary>
/// Immutable snapshot of a fully configured report, ready for export.
/// This is the only type that crosses the builder → exporter boundary.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed record ReportDefinition<T>
{
    /// <summary>Report title (used in file headers, sheet names, etc.).</summary>
    public required string Title { get; init; }

    /// <summary>Ordered column definitions.</summary>
    public required IReadOnlyList<ColumnDefinition<T>> Columns { get; init; }

    /// <summary>Materialized row data.</summary>
    public required IReadOnlyList<T> Data { get; init; }

    /// <summary>UTC timestamp when the definition was built.</summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
