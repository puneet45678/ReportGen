using ClosedXML.Excel;

namespace ReportGen.Exporters.Internal;

/// <summary>
/// Type-erased descriptor for a single worksheet in a multi-sheet workbook.
/// Captures the typed sheet data and column definitions at AddSheet time.
/// </summary>
internal interface IExcelSheetDescriptor
{
    void WriteToWorksheet(IXLWorksheet worksheet, CancellationToken cancellationToken);
}
