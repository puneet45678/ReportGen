using System.Globalization;
using System.Text;
using CsvHelper;
using ReportGen.Core;

namespace ReportGen.Exporters;

/// <summary>
/// Exports a report to CSV using CsvHelper.
/// </summary>
public sealed class CsvExporter : IReportExporter
{
    private readonly string? _filePath;
    private readonly Stream? _stream;
    private readonly CultureInfo _culture;

    /// <summary>
    /// Creates a CSV exporter that writes to the specified file path.
    /// </summary>
    /// <param name="filePath">Destination file path. Directory is created if missing.</param>
    /// <param name="culture">
    /// Culture used for number and date serialization. Defaults to
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// <para>
    /// Note: CsvHelper sets the CSV delimiter from the culture's <c>TextInfo.ListSeparator</c>.
    /// For <c>de-DE</c> and other European locales this produces a semicolon-delimited file
    /// rather than comma-delimited. This is correct behaviour for those locales.
    /// </para>
    /// <para>
    /// Column-level <see cref="ColumnDefinition{T}.ExcelFormat"/> strings are ignored by CSV exporters.
    /// </para>
    /// </param>
    public CsvExporter(string filePath, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Creates a CSV exporter that writes to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    /// <param name="stream">Destination stream. Must be writable.</param>
    /// <param name="culture">
    /// Culture used for number and date serialization. Defaults to
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </param>
    public CsvExporter(Stream stream, CultureInfo? culture = null)
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

        var writer = _filePath is not null
            ? new StreamWriter(_filePath, append: false, Encoding.UTF8)
            : new StreamWriter(_stream!, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);

        await using var _ = writer;
        await using var csv = new CsvWriter(writer, _culture);

        // Header row
        foreach (var column in report.Columns)
        {
            csv.WriteField(column.Header);
        }
        await csv.NextRecordAsync().ConfigureAwait(false);

        // Data rows
        foreach (var row in report.Data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var column in report.Columns)
            {
                csv.WriteField(column.Accessor(row));
            }
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await csv.FlushAsync().ConfigureAwait(false);
    }
}
