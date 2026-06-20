using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportGen.Core;

namespace ReportGen.Exporters.Internal;

internal sealed class PdfDocumentDescriptor<T> : IDocument
{
    private readonly ReportDefinition<T> _report;
    private readonly PdfExportOptions _options;

    internal PdfDocumentDescriptor(ReportDefinition<T> report, PdfExportOptions options)
    {
        _report = report;
        _options = options;
    }

    public DocumentMetadata GetMetadata() => new() { Title = _report.Title };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer documentContainer)
    {
        documentContainer.Page(page =>
        {
            var baseSize = _options.PageSize switch
            {
                PdfPageSize.A4     => PageSizes.A4,
                PdfPageSize.Letter => PageSizes.Letter,
                PdfPageSize.A3     => PageSizes.A3,
                _                  => PageSizes.A4
            };

            var pageSize = _options.Landscape
                ? new PageSize(baseSize.Height, baseSize.Width)
                : baseSize;

            page.Size(pageSize);
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            if (_options.ShowTitle)
            {
                page.Header()
                    .PaddingBottom(0.5f, Unit.Centimetre)
                    .Text(_report.Title)
                    .SemiBold()
                    .FontSize(16);
            }

            page.Content()
                .Element(c => PdfTableRenderer.Render(c, _report, _options));

            BuildFooter(page);
        });
    }

    private void BuildFooter(PageDescriptor page)
    {
        if (!_options.ShowGeneratedAt && !_options.ShowPageNumbers)
            return;

        page.Footer()
            .AlignCenter()
            .Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(9).FontColor("#6B7280"));

                if (_options.ShowGeneratedAt)
                    text.Span($"Generated: {_report.GeneratedAtUtc:dd MMM yyyy HH:mm} UTC");

                if (_options.ShowGeneratedAt && _options.ShowPageNumbers)
                    text.Span("  |  ");

                if (_options.ShowPageNumbers)
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                }
            });
    }
}
