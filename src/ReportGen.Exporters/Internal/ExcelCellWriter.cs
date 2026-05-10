using System.Globalization;
using ClosedXML.Excel;

namespace ReportGen.Exporters.Internal;

internal static class ExcelCellWriter
{
    /// <summary>
    /// Writes a CLR value into an Excel cell using type-appropriate ClosedXML value types.
    /// <paramref name="culture"/> is used only for the ToString() fallback on unrecognized types.
    /// </summary>
    public static void SetCellValue(IXLCell cell, object? value, CultureInfo culture)
    {
        cell.Value = value switch
        {
            null            => Blank.Value,
            string s        => s,
            int i           => i,
            long l          => l,
            double d        => d,
            decimal m       => m,
            float f         => f,
            DateTime dt     => dt,
            DateTimeOffset dto => dto.DateTime,
            DateOnly date   => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time   => DateTime.Today.Add(time.ToTimeSpan()),
            bool b          => b,
            short sh        => (int)sh,
            uint u          => (long)u,
            byte by         => (int)by,
            Guid g          => g.ToString(),
            _               => Convert.ToString(value, culture) ?? string.Empty
        };
    }
}
