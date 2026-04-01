namespace ReportGen.Core;

/// <summary>
/// Contract for all report format exporters (CSV, Excel, PDF, custom, etc.).
/// Generic parameter is on the method — one exporter instance handles any T.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Exports the report definition to the target format/destination.
    /// </summary>
    Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default);
}
