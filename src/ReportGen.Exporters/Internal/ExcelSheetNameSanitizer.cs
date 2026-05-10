namespace ReportGen.Exporters.Internal;

internal static class ExcelSheetNameSanitizer
{
    private static readonly char[] InvalidChars = ['[', ']', ':', '*', '?', '/', '\\'];

    /// <summary>
    /// Sanitizes a string for use as an Excel worksheet name.
    /// Truncates to 31 characters and replaces invalid characters with underscores.
    /// </summary>
    public static string Sanitize(string title)
    {
        var sanitized = title.Length > 31 ? title[..31] : title;
        foreach (var c in InvalidChars)
            sanitized = sanitized.Replace(c, '_');
        return sanitized;
    }
}
