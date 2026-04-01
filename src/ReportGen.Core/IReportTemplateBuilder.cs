namespace ReportGen.Core;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="ReportTemplate{T}"/>.
/// </summary>
/// <typeparam name="T">The row data type this template is designed for.</typeparam>
public interface IReportTemplateBuilder<T>
{
    /// <summary>Adds a column definition to the template.</summary>
    IReportTemplateBuilder<T> AddColumn(string header, Func<T, object?> accessor);

    /// <summary>Builds the immutable, reusable template.</summary>
    ReportTemplate<T> Build();
}
