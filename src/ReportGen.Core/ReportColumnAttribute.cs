namespace ReportGen.Core;

/// <summary>
/// Marks a property for automatic inclusion in a report.
/// Use with <c>.AddColumnsFromAttributes()</c> on the builder or template builder.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ReportColumnAttribute : Attribute
{
    /// <summary>
    /// Column header text. If null or empty, the property name is used.
    /// </summary>
    public string? Header { get; }

    /// <summary>
    /// Column display order. Lower numbers appear first.
    /// Default is <see cref="int.MaxValue"/> (appended after explicitly ordered columns).
    /// </summary>
    public int Order { get; set; } = int.MaxValue;

    /// <summary>
    /// Marks this property as a report column using the property name as the header.
    /// </summary>
    public ReportColumnAttribute() { }

    /// <summary>
    /// Marks this property as a report column with a custom header.
    /// </summary>
    /// <param name="header">Column header text displayed in the report.</param>
    public ReportColumnAttribute(string header) => Header = header;
}
