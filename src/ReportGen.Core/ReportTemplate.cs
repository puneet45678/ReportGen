namespace ReportGen.Core;

/// <summary>
/// Immutable, reusable report shape. Thread-safe — can be registered as a DI singleton
/// and shared across requests. Captures title + columns; data is bound later via From().
/// </summary>
/// <typeparam name="T">The row data type this template is designed for.</typeparam>
public sealed class ReportTemplate<T>
{
    /// <summary>Default report title.</summary>
    public string Title { get; }

    /// <summary>Column definitions (frozen at build time).</summary>
    public IReadOnlyList<ColumnDefinition<T>> Columns { get; }

    internal ReportTemplate(string title, IReadOnlyList<ColumnDefinition<T>> columns)
    {
        Title = title;
        Columns = columns;
    }

    /// <summary>
    /// Binds data to this template using the default title.
    /// Returns an independent builder — the template is NOT modified.
    /// </summary>
    public IReportBuilder<T> From(IEnumerable<T> data)
        => From(data, title: null);

    /// <summary>
    /// Binds data with an optional title override (e.g., "Sales — March 2026").
    /// If <paramref name="title"/> is null or whitespace, the template's default title is used.
    /// </summary>
    public IReportBuilder<T> From(IEnumerable<T> data, string? title)
    {
        ArgumentNullException.ThrowIfNull(data);
        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? Title : title;
        var builder = new Internal.ReportBuilder<T>(effectiveTitle, data);
        foreach (var column in Columns)
            builder.AddColumn(column.Header, column.Accessor);
        return builder;
    }

    /// <summary>
    /// Entry point for defining reusable report templates.
    /// T must be specified explicitly since there is no data to infer from.
    /// </summary>
    public static IReportTemplateBuilder<T> Define(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Internal.ReportTemplateBuilder<T>(title);
    }
}
