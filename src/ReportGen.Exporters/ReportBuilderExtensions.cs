using ReportGen.Core;

namespace ReportGen.Exporters;

/// <summary>
/// Fluent extension methods that provide .ToCsv() and .ToExcel() sugar
/// on top of IReportBuilder&lt;T&gt;.AddExporter().
/// </summary>
public static class ReportBuilderExtensions
{
    /// <summary>
    /// Registers a CSV exporter writing to the specified file path.
    /// </summary>
    public static IReportBuilder<T> ToCsv<T>(this IReportBuilder<T> builder, string filePath)
        => builder.AddExporter(new CsvExporter(filePath));

    /// <summary>
    /// Registers an Excel exporter writing to the specified file path.
    /// </summary>
    public static IReportBuilder<T> ToExcel<T>(this IReportBuilder<T> builder, string filePath)
        => builder.AddExporter(new ExcelExporter(filePath));
}
