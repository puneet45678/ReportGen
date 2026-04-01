using System.Reflection;

namespace ReportGen.Core;

/// <summary>
/// Extension methods for automatic column discovery from <see cref="ReportColumnAttribute"/>.
/// </summary>
public static class ReportColumnExtensions
{
    /// <summary>
    /// Discovers properties on <typeparamref name="T"/> decorated with
    /// <see cref="ReportColumnAttribute"/> and adds them as columns.
    /// Can be mixed with manual <c>.AddColumn()</c> calls.
    /// </summary>
    public static IReportBuilder<T> AddColumnsFromAttributes<T>(this IReportBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (header, accessor) in DiscoverColumns<T>())
            builder.AddColumn(header, accessor);
        return builder;
    }

    /// <summary>
    /// Discovers properties on <typeparamref name="T"/> decorated with
    /// <see cref="ReportColumnAttribute"/> and adds them as columns to the template.
    /// Can be mixed with manual <c>.AddColumn()</c> calls.
    /// </summary>
    public static IReportTemplateBuilder<T> AddColumnsFromAttributes<T>(this IReportTemplateBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (header, accessor) in DiscoverColumns<T>())
            builder.AddColumn(header, accessor);
        return builder;
    }

    private static IEnumerable<(string Header, Func<T, object?> Accessor)> DiscoverColumns<T>()
    {
        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<ReportColumnAttribute>()))
            .Where(x => x.Attribute is not null)
            .OrderBy(x => x.Attribute!.Order)
            .ThenBy(x => x.Property.MetadataToken)
            .ToList();

        if (properties.Count == 0)
            throw new InvalidOperationException(
                $"No properties on '{typeof(T).Name}' are decorated with [ReportColumn]. " +
                $"Add [ReportColumn] to at least one public property.");

        foreach (var (property, attribute) in properties)
        {
            var header = string.IsNullOrWhiteSpace(attribute!.Header)
                ? property.Name
                : attribute.Header;

            var prop = property;
            Func<T, object?> accessor = row => prop.GetValue(row);

            yield return (header, accessor);
        }
    }
}
