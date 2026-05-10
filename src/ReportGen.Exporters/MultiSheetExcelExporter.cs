using System.Globalization;
using ClosedXML.Excel;
using ReportGen.Core;
using ReportGen.Exporters.Internal;

namespace ReportGen.Exporters;

/// <summary>
/// Builds and exports a multi-sheet Excel workbook.
/// Each sheet has its own typed dataset and column definitions.
/// </summary>
/// <example>
/// <code>
/// await new MultiSheetExcelExporter("annual.xlsx")
///     .AddSheet("Sales", salesData, b => b
///         .AddColumn("Product", x => x.Product)
///         .AddColumn("Revenue", x => x.Revenue, "$#,##0.00"))
///     .AddSheet("Expenses", expenseData, b => b
///         .AddColumn("Category", x => x.Category)
///         .AddColumn("Amount",   x => x.Amount, "$#,##0.00"))
///     .WriteAsync(cancellationToken);
/// </code>
/// </example>
public sealed class MultiSheetExcelExporter : IReportExporter
{
    private readonly string? _filePath;
    private readonly Stream? _stream;
    private readonly CultureInfo _culture;
    private readonly List<(string SheetName, IExcelSheetDescriptor Descriptor)> _sheets = [];

    /// <summary>
    /// Creates a multi-sheet exporter that writes to the specified .xlsx file path.
    /// </summary>
    /// <param name="filePath">Destination file path. Directory is created if missing.</param>
    /// <param name="culture">
    /// Culture used for the ToString() fallback on unrecognised cell value types.
    /// Applied to every sheet. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </param>
    public MultiSheetExcelExporter(string filePath, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Creates a multi-sheet exporter that writes to the provided stream.
    /// The stream must be writable and seekable (ClosedXML requirement).
    /// The caller retains ownership of and is responsible for disposing the stream.
    /// </summary>
    /// <param name="stream">Destination stream.</param>
    /// <param name="culture">
    /// Culture used for the ToString() fallback. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </param>
    public MultiSheetExcelExporter(Stream stream, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Adds a sheet to the workbook. The sheet title is used as the worksheet name
    /// (sanitized to comply with Excel's 31-character / no-special-characters rule).
    /// </summary>
    /// <typeparam name="T">Row data type for this sheet.</typeparam>
    /// <param name="title">Sheet title, used as the worksheet tab name.</param>
    /// <param name="data">Data rows for this sheet.</param>
    /// <param name="configure">
    /// Builder configuration action — call <c>.AddColumn()</c> here to define columns.
    /// </param>
    /// <returns>This exporter, for fluent chaining.</returns>
    public MultiSheetExcelExporter AddSheet<T>(
        string title,
        IEnumerable<T> data,
        Action<IReportBuilder<T>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Report.Create(title).From(data);
        configure(builder);
        var definition = builder.Build();
        var sheetName = ExcelSheetNameSanitizer.Sanitize(definition.Title);

        _sheets.Add((sheetName, new ExcelSheetDescriptor<T>(definition, _culture)));
        return this;
    }

    /// <summary>
    /// Builds the workbook and writes all sheets to the configured destination.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no sheets have been added, or when two sheets produce the same
    /// sanitized worksheet name.
    /// </exception>
    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        if (_sheets.Count == 0)
            throw new InvalidOperationException(
                "At least one sheet must be added before calling WriteAsync.");

        var names = _sheets.Select(s => s.SheetName).ToList();
        var duplicates = names
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"The following worksheet names are duplicated after sanitization: " +
                $"{string.Join(", ", duplicates)}. Use distinct sheet titles.");

        if (_filePath is not null)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        using var workbook = new XLWorkbook();

        foreach (var (sheetName, descriptor) in _sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ws = workbook.Worksheets.Add(sheetName);
            descriptor.WriteToWorksheet(ws, cancellationToken);
        }

        await Task.Run(() =>
        {
            if (_filePath is not null)
                workbook.SaveAs(_filePath);
            else
                workbook.SaveAs(_stream!);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Not supported. Use <see cref="AddSheet{T}"/> followed by <see cref="WriteAsync"/> instead.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    Task IReportExporter.ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            $"{nameof(MultiSheetExcelExporter)} does not support the single-report ExportAsync contract. " +
            $"Use AddSheet() to configure sheets and then call WriteAsync().");
}
