using ClosedXML.Excel;
using ReportGen.Core;

namespace ReportGen.Exporters;

/// <summary>
/// Exports a report to an Excel .xlsx file using ClosedXML.
/// </summary>
public sealed class ExcelExporter : IReportExporter
{
    private readonly string? _filePath;
    private readonly Stream? _stream;

    /// <summary>
    /// Creates an Excel exporter that writes to the specified .xlsx file path.
    /// </summary>
    /// <param name="filePath">Destination file path. Directory is created if missing.</param>
    public ExcelExporter(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Creates an Excel exporter that writes to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    /// <param name="stream">Destination stream. Must be writable and seekable.</param>
    public ExcelExporter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
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
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(report.Title));

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
                var value = report.Columns[col].Accessor(row);
                var cell = worksheet.Cell(rowIdx + 2, col + 1);
                SetCellValue(cell, value);
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

    private static string SanitizeSheetName(string title)
    {
        // Excel sheet names: max 31 chars, no []:*?/\
        var sanitized = title.Length > 31 ? title[..31] : title;
        foreach (var c in new[] { '[', ']', ':', '*', '?', '/', '\\' })
            sanitized = sanitized.Replace(c, '_');
        return sanitized;
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        cell.Value = value switch
        {
            null => Blank.Value,
            string s => s,
            int i => i,
            long l => l,
            double d => d,
            decimal m => m,
            float f => f,
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => DateTime.Today.Add(time.ToTimeSpan()),
            bool b => b,
            short sh => (int)sh,
            uint u => (long)u,
            byte by => (int)by,
            Guid g => g.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }
}
