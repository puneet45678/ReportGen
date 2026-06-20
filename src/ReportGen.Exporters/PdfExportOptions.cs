namespace ReportGen.Exporters;

/// <summary>
/// Page size options for PDF export.
/// </summary>
public enum PdfPageSize
{
    /// <summary>ISO A4 (210 × 297 mm).</summary>
    A4,
    /// <summary>US Letter (8.5 × 11 in).</summary>
    Letter,
    /// <summary>ISO A3 (297 × 420 mm).</summary>
    A3
}

/// <summary>
/// Controls the visual appearance and layout of a generated PDF report.
/// All properties have sensible defaults — override only what you need.
/// </summary>
public sealed class PdfExportOptions
{
    /// <summary>Page size. Defaults to A4.</summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    /// <summary>When <see langword="true"/>, the page is rendered in landscape orientation.</summary>
    public bool Landscape { get; set; } = false;

    /// <summary>Background color of the header row as a hex string (e.g. <c>"#2563EB"</c>).</summary>
    public string HeaderBackgroundColor { get; set; } = "#2563EB";

    /// <summary>Text color of the header row as a hex string.</summary>
    public string HeaderTextColor { get; set; } = "#FFFFFF";

    /// <summary>Background color applied to every odd-indexed data row as a hex string.</summary>
    public string AlternateRowColor { get; set; } = "#F3F4F6";

    /// <summary>When <see langword="true"/>, renders the report title above the table.</summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>When <see langword="true"/>, renders the current page number in the footer.</summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>When <see langword="true"/>, renders the UTC generation timestamp in the footer.</summary>
    public bool ShowGeneratedAt { get; set; } = true;
}
