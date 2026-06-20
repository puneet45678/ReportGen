using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ReportGen.Core;
using ReportGen.Exporters.Internal;

namespace ReportGen.Exporters;

/// <summary>
/// Exports a report to a PDF file using QuestPDF.
/// </summary>
public sealed class PdfExporter : IReportExporter
{
    private readonly string? _filePath;
    private readonly Stream? _stream;
    private readonly PdfExportOptions _options;

    /// <summary>
    /// Creates a PDF exporter that writes to the specified file path.
    /// </summary>
    /// <param name="filePath">Destination file path. Directory is created if missing.</param>
    /// <param name="options">Optional layout/style overrides. Defaults apply when <see langword="null"/>.</param>
    public PdfExporter(string filePath, PdfExportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _options = options ?? new PdfExportOptions();
    }

    /// <summary>
    /// Creates a PDF exporter that writes to the provided stream.
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    /// <param name="stream">Destination stream. Must be writable.</param>
    /// <param name="options">Optional layout/style overrides. Defaults apply when <see langword="null"/>.</param>
    public PdfExporter(Stream stream, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _options = options ?? new PdfExportOptions();
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

        QuestPDF.Settings.License = LicenseType.Community;

        var descriptor = new PdfDocumentDescriptor<T>(report, _options);

        // QuestPDF generation is synchronous — offload to avoid blocking the caller
        await Task.Run(() =>
        {
            if (_filePath is not null)
                descriptor.GeneratePdf(_filePath);
            else
                descriptor.GeneratePdf(_stream!);
        }, cancellationToken).ConfigureAwait(false);
    }
}
