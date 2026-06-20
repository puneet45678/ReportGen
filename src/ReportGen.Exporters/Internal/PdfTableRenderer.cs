using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ReportGen.Core;

namespace ReportGen.Exporters.Internal;

internal static class PdfTableRenderer
{
    internal static void Render<T>(IContainer container, ReportDefinition<T> report, PdfExportOptions options)
    {
        var summaryValues = SummaryRowRenderer.ComputeValues(report);

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                for (var i = 0; i < report.Columns.Count; i++)
                    cols.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var col in report.Columns)
                {
                    header.Cell()
                        .Background(options.HeaderBackgroundColor)
                        .Padding(5)
                        .Text(text => text.Span(col.Header)
                            .Bold()
                            .FontColor(options.HeaderTextColor));
                }
            });

            for (var rowIdx = 0; rowIdx < report.Data.Count; rowIdx++)
            {
                var row = report.Data[rowIdx];
                var bgColor = rowIdx % 2 == 0 ? "#FFFFFF" : options.AlternateRowColor;

                foreach (var col in report.Columns)
                {
                    var value = col.Accessor(row)?.ToString() ?? "";
                    table.Cell()
                        .Background(bgColor)
                        .Padding(4)
                        .Text(value);
                }
            }

            if (summaryValues is not null)
            {
                for (var col = 0; col < report.Columns.Count; col++)
                {
                    var value = summaryValues[col]?.ToString() ?? "";
                    table.Cell()
                        .BorderTop(1f)
                        .BorderColor("#9CA3AF")
                        .Background("#F9FAFB")
                        .Padding(4)
                        .Text(text => text.Span(value).Bold());
                }
            }
        });
    }
}
