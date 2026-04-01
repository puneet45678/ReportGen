namespace ReportGen.Core;

/// <summary>
/// Pre-data builder stage. Type T is unknown until .From() binds it.
/// This enforces that .From() must be called before .AddColumn().
/// </summary>
public interface IReportBuilderSource
{
    /// <summary>
    /// Binds data to the report. T is inferred from the collection's element type.
    /// Transitions to the typed <see cref="IReportBuilder{T}"/>.
    /// </summary>
    IReportBuilder<T> From<T>(IEnumerable<T> data);
}
