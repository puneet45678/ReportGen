namespace ReportGen.Core.Internal;

/// <summary>
/// Builder for <see cref="ReportTemplate{T}"/>.
/// Internal — consumers interact through <see cref="IReportTemplateBuilder{T}"/>.
/// </summary>
internal sealed class ReportTemplateBuilder<T> : IReportTemplateBuilder<T>
{
    private readonly string _title;
    private readonly List<ColumnDefinition<T>> _columns = [];
    private int _columnOrder;

    internal ReportTemplateBuilder(string title) => _title = title;

    public IReportTemplateBuilder<T> AddColumn(string header, Func<T, object?> accessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentNullException.ThrowIfNull(accessor);
        _columns.Add(new ColumnDefinition<T>(header, accessor, _columnOrder++));
        return this;
    }

    public ReportTemplate<T> Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be defined.");

        return new ReportTemplate<T>(
            _title,
            _columns.OrderBy(c => c.Order).ToList().AsReadOnly());
    }
}
