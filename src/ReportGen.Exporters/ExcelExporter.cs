using System.Globalization;
using ClosedXML.Excel;
using ReportGen.Core;
using ReportGen.Exporters.Internal;

namespace ReportGen.Exporters;

/// <summary>
/// Exports a report to an Excel .xlsx file using ClosedXML.
/// </summary>
public sealed class ExcelExporter : IReportExporter
{
    private readonly string? _filePath;
    private readonly Stream? _stream;
    private readonly CultureInfo _culture;

    /// <summary>
    /// Creates an Excel exporter that writes to the specified .xlsx file path.
    /// </summary>
    /// <param name="filePath">Destination file path. Directory is created if missing.</param>
    /// <param name="culture">
    /// Culture used for the ToString() fallback when a cell value is an unrecognised type.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// Note: ClosedXML writes numbers in OOXML as invariant-culture XML regardless of this value.
    /// For locale-specific number display in Excel cells, set a locale-appropriate
    /// <see cref="ColumnDefinition{T}.ExcelFormat"/> on the column instead.
    /// </param>
    public ExcelExporter(string filePath, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Creates an Excel exporter that writes to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// The stream must be writable and seekable (ClosedXML requirement).
    /// </summary>
    /// <param name="stream">Destination stream.</param>
    /// <param name="culture">
    /// Culture used for the ToString() fallback on unrecognised types.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </param>
    public ExcelExporter(Stream stream, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc />
    public async Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default)
    {
        if (_filePath is not null)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(ExcelSheetNameSanitizer.Sanitize(report.Title));

        // Header row
        for (var col = 0; col < report.Columns.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = report.Columns[col].Header;
            cell.Style.Font.Bold = true;
        }

        // Data rows
        for (var rowIdx = 0; rowIdx < report.Data.Count; rowIdx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = report.Data[rowIdx];
            for (var col = 0; col < report.Columns.Count; col++)
            {
                var column = report.Columns[col];
                var cell = worksheet.Cell(rowIdx + 2, col + 1);
                ExcelCellWriter.SetCellValue(cell, column.Accessor(row), _culture);
                if (column.ExcelFormat is { Length: > 0 } fmt)
                    cell.Style.NumberFormat.Format = fmt;
            }
        }

        worksheet.Columns().AdjustToContents();

        // ClosedXML SaveAs is synchronous — offload to avoid blocking the caller
        await Task.Run(() =>
        {
            if (_filePath is not null)
                workbook.SaveAs(_filePath);
            else
                workbook.SaveAs(_stream!);
        }, cancellationToken).ConfigureAwait(false);
    }
}
