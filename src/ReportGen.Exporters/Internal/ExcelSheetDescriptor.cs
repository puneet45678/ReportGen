using System.Globalization;
using ClosedXML.Excel;
using ReportGen.Core;

namespace ReportGen.Exporters.Internal;

/// <summary>
/// Closed-generic implementation of <see cref="IExcelSheetDescriptor"/> that captures
/// a typed <see cref="ReportDefinition{T}"/> and renders it to a worksheet on demand.
/// </summary>
internal sealed class ExcelSheetDescriptor<T> : IExcelSheetDescriptor
{
    private readonly ReportDefinition<T> _definition;
    private readonly CultureInfo _culture;

    internal ExcelSheetDescriptor(ReportDefinition<T> definition, CultureInfo culture)
    {
        _definition = definition;
        _culture = culture;
    }

    public void WriteToWorksheet(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        // Header row
        for (var col = 0; col < _definition.Columns.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = _definition.Columns[col].Header;
            cell.Style.Font.Bold = true;
        }

        // Data rows
        for (var rowIdx = 0; rowIdx < _definition.Data.Count; rowIdx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = _definition.Data[rowIdx];
            for (var col = 0; col < _definition.Columns.Count; col++)
            {
                var column = _definition.Columns[col];
                var cell = worksheet.Cell(rowIdx + 2, col + 1);
                ExcelCellWriter.SetCellValue(cell, column.Accessor(row), _culture);
                if (column.ExcelFormat is { Length: > 0 } fmt)
                    cell.Style.NumberFormat.Format = fmt;
            }
        }

        // Summary row (optional)
        var summaryValues = SummaryRowRenderer.ComputeValues(_definition);
        if (summaryValues is not null)
        {
            var summaryRowIdx = _definition.Data.Count + 2;
            for (var col = 0; col < _definition.Columns.Count; col++)
            {
                var column = _definition.Columns[col];
                var cell = worksheet.Cell(summaryRowIdx, col + 1);
                ExcelCellWriter.SetCellValue(cell, summaryValues[col], _culture);
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                if (column.ExcelFormat is { Length: > 0 } fmt)
                    cell.Style.NumberFormat.Format = fmt;
            }
        }

        worksheet.Columns().AdjustToContents();
    }
}
