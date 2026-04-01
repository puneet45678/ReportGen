namespace ReportGen.Core.Internal;

/// <summary>
/// Phase 2 builder: typed, collects columns and exporters, produces ReportDefinition.
/// Internal — consumers interact through <see cref="IReportBuilder{T}"/>.
/// </summary>
internal sealed class ReportBuilder<T> : IReportBuilder<T>
{
    private readonly string _title;
    private readonly IEnumerable<T> _data;
    private readonly List<ColumnDefinition<T>> _columns = [];
    private readonly List<IReportExporter> _exporters = [];
    private int _columnOrder;

    internal ReportBuilder(string title, IEnumerable<T> data)
    {
        _title = title;
        _data = data;
    }

    /// <summary>
    /// Adds columns — can also be called by ReportTemplate.From() to pre-populate.
    /// </summary>
    public IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentNullException.ThrowIfNull(accessor);
        _columns.Add(new ColumnDefinition<T>(header, accessor, _columnOrder++));
        return this;
    }

    public IReportBuilder<T> AddExporter(IReportExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        _exporters.Add(exporter);
        return this;
    }

    public ReportDefinition<T> Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be defined.");

        return new ReportDefinition<T>
        {
            Title = _title,
            Columns = _columns.OrderBy(c => c.Order).ToList().AsReadOnly(),
            Data = _data.ToList().AsReadOnly()
        };
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (_exporters.Count == 0)
            throw new InvalidOperationException("At least one exporter must be registered.");

        var definition = Build();

        foreach (var exporter in _exporters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await exporter.ExportAsync(definition, cancellationToken).ConfigureAwait(false);
        }
    }
}
