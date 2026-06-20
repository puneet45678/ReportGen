using System.Globalization;
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
    public static IReportBuilder<T> ToCsv<T>(this IReportBuilder<T> builder, string filePath,
        CultureInfo? culture = null)
        => builder.AddExporter(new CsvExporter(filePath, culture));

    /// <summary>
    /// Registers a CSV exporter writing to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    public static IReportBuilder<T> ToCsv<T>(this IReportBuilder<T> builder, Stream stream,
        CultureInfo? culture = null)
        => builder.AddExporter(new CsvExporter(stream, culture));

    /// <summary>
    /// Registers an Excel exporter writing to the specified file path.
    /// </summary>
    public static IReportBuilder<T> ToExcel<T>(this IReportBuilder<T> builder, string filePath,
        CultureInfo? culture = null)
        => builder.AddExporter(new ExcelExporter(filePath, culture));

    /// <summary>
    /// Registers an Excel exporter writing to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    public static IReportBuilder<T> ToExcel<T>(this IReportBuilder<T> builder, Stream stream,
        CultureInfo? culture = null)
        => builder.AddExporter(new ExcelExporter(stream, culture));

    /// <summary>
    /// Registers a PDF exporter writing to the specified file path.
    /// </summary>
    public static IReportBuilder<T> ToPdf<T>(this IReportBuilder<T> builder, string filePath,
        PdfExportOptions? options = null)
        => builder.AddExporter(new PdfExporter(filePath, options));

    /// <summary>
    /// Registers a PDF exporter writing to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    public static IReportBuilder<T> ToPdf<T>(this IReportBuilder<T> builder, Stream stream,
        PdfExportOptions? options = null)
        => builder.AddExporter(new PdfExporter(stream, options));
}
