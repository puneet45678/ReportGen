namespace ReportGen.Core.Internal;

/// <summary>
/// Internal implementation of <see cref="ISummaryRowBuilder{T}"/>.
/// Validates column references eagerly and builds aggregation closures at configuration time.
/// </summary>
internal sealed class SummaryRowBuilder<T> : ISummaryRowBuilder<T>
{
    private readonly IReportBuilder<T> _parent;
    private readonly IReadOnlyList<ColumnDefinition<T>> _columns;

    // Maps column header → compute delegate. Only explicitly configured columns are stored.
    // Build() fills blanks (null-returning delegates) for any column not present.
    private readonly Dictionary<string, Func<IReadOnlyList<T>, object?>> _cells = new();

    internal SummaryRowBuilder(IReportBuilder<T> parent, IReadOnlyList<ColumnDefinition<T>> columns)
    {
        _parent  = parent;
        _columns = columns;
    }

    public ISummaryRowBuilder<T> Set(string columnHeader, object? value)
    {
        ValidateHeader(columnHeader);
        _cells[columnHeader] = _ => value;
        return this;
    }

    public ISummaryRowBuilder<T> Sum(string columnHeader)
    {
        ValidateHeader(columnHeader);
        var col = FindColumn(columnHeader);
        _cells[columnHeader] = data =>
        {
            decimal sum = 0;
            var any = false;
            foreach (var row in data)
            {
                var v = col.Accessor(row);
                if (v is null) continue;
                sum += ToDecimal(v, columnHeader);
                any = true;
            }
            return any ? (object?)sum : null;
        };
        return this;
    }

    public ISummaryRowBuilder<T> Average(string columnHeader)
    {
        ValidateHeader(columnHeader);
        var col = FindColumn(columnHeader);
        _cells[columnHeader] = data =>
        {
            decimal sum = 0;
            var count = 0;
            foreach (var row in data)
            {
                var v = col.Accessor(row);
                if (v is null) continue;
                sum += ToDecimal(v, columnHeader);
                count++;
            }
            return count > 0 ? (object?)(sum / count) : null;
        };
        return this;
    }

    public ISummaryRowBuilder<T> Count(string columnHeader)
    {
        ValidateHeader(columnHeader);
        var col = FindColumn(columnHeader);
        _cells[columnHeader] = data =>
        {
            var count = 0;
            foreach (var row in data)
                if (col.Accessor(row) is not null) count++;
            return (object?)count;
        };
        return this;
    }

    public ISummaryRowBuilder<T> Min(string columnHeader)
    {
        ValidateHeader(columnHeader);
        var col = FindColumn(columnHeader);
        _cells[columnHeader] = data =>
        {
            decimal? min = null;
            foreach (var row in data)
            {
                var v = col.Accessor(row);
                if (v is null) continue;
                var d = ToDecimal(v, columnHeader);
                min = min is null ? d : (d < min.Value ? d : min);
            }
            return min;
        };
        return this;
    }

    public ISummaryRowBuilder<T> Max(string columnHeader)
    {
        ValidateHeader(columnHeader);
        var col = FindColumn(columnHeader);
        _cells[columnHeader] = data =>
        {
            decimal? max = null;
            foreach (var row in data)
            {
                var v = col.Accessor(row);
                if (v is null) continue;
                var d = ToDecimal(v, columnHeader);
                max = max is null ? d : (d > max.Value ? d : max);
            }
            return max;
        };
        return this;
    }

    public ISummaryRowBuilder<T> Blank(string columnHeader)
    {
        ValidateHeader(columnHeader);
        _cells[columnHeader] = _ => null;
        return this;
    }

    public ISummaryRowBuilder<T> Compute(string columnHeader, Func<IReadOnlyList<T>, object?> compute)
    {
        ArgumentNullException.ThrowIfNull(compute);
        ValidateHeader(columnHeader);
        _cells[columnHeader] = compute;
        return this;
    }

    /// <summary>
    /// Materializes the ordered list of <see cref="SummaryCellDefinition{T}"/> —
    /// one per column, filling null-returning delegates for any column not explicitly configured.
    /// </summary>
    internal IReadOnlyList<SummaryCellDefinition<T>> Build()
        => _columns
            .OrderBy(c => c.Order)
            .Select(c => new SummaryCellDefinition<T>(
                c.Header,
                _cells.TryGetValue(c.Header, out var fn) ? fn : _ => null))
            .ToList()
            .AsReadOnly();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ValidateHeader(string columnHeader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnHeader);
        if (!_columns.Any(c => c.Header == columnHeader))
            throw new ArgumentException(
                $"Column '{columnHeader}' does not exist. " +
                $"Available columns: {string.Join(", ", _columns.Select(c => $"'{c.Header}'"))}.",
                nameof(columnHeader));
    }

    private ColumnDefinition<T> FindColumn(string header)
        => _columns.First(c => c.Header == header);

    private static decimal ToDecimal(object value, string columnHeader)
    {
        try
        {
            return Convert.ToDecimal(value);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidCastException(
                $"Cannot aggregate column '{columnHeader}': " +
                $"value '{value}' (type: {value.GetType().Name}) is not numeric. " +
                $"Sum, Average, Min, and Max require numeric columns. " +
                $"Use Compute() for custom aggregation on non-numeric types.", ex);
        }
    }
}
