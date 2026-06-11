using ReportGen.Core;

namespace ReportGen.Exporters.Internal;

/// <summary>
/// Evaluates all summary cell compute delegates for a report and returns
/// the results as an ordered array, or <see langword="null"/> when no summary row is defined.
/// </summary>
internal static class SummaryRowRenderer
{
    /// <summary>
    /// Returns <see langword="null"/> when the report has no summary row.
    /// Otherwise returns an <c>object?[]</c> of length <c>report.Columns.Count</c>
    /// where index <c>i</c> corresponds to <c>report.Columns[i]</c>.
    /// </summary>
    internal static object?[]? ComputeValues<T>(ReportDefinition<T> report)
    {
        if (report.SummaryRow is null)
            return null;

        var values = new object?[report.Columns.Count];
        for (var i = 0; i < report.Columns.Count; i++)
            values[i] = report.SummaryRow[i].Compute(report.Data);

        return values;
    }
}
