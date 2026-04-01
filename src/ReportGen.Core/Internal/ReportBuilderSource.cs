namespace ReportGen.Core.Internal;

/// <summary>
/// Phase 1 builder: holds the title, waits for .From(data) to bind T.
/// Internal — consumers interact through <see cref="IReportBuilderSource"/>.
/// </summary>
internal sealed class ReportBuilderSource : IReportBuilderSource
{
    private readonly string _title;

    internal ReportBuilderSource(string title) => _title = title;

    public IReportBuilder<T> From<T>(IEnumerable<T> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ReportBuilder<T>(_title, data);
    }
}
