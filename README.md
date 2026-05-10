# ReportGen

A modular, extensible .NET 8 report generation library with a fluent builder API.

[![CI](https://github.com/puneet45678/ReportGen/actions/workflows/ci.yml/badge.svg)](https://github.com/puneet45678/ReportGen/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Why ReportGen?

Most teams need the same report capabilities repeatedly:
- Build tabular reports from object collections
- Export to multiple formats (CSV, Excel — more coming)
- Keep the API simple and strongly typed
- Extend delivery/execution later (email, queue workers)

ReportGen provides this with a clean fluent API and package-first developer experience.

## Installation

```bash
dotnet add package ReportGen.Core --version 0.1.0
dotnet add package ReportGen.Exporters --version 0.1.0
```

## Quick Start

### Fluent builder (full control)

```csharp
using ReportGen.Core;
using ReportGen.Exporters;

var users = new[]
{
    new { Name = "Ava", Email = "ava@company.com", Score = 92 },
    new { Name = "Noah", Email = "noah@company.com", Score = 88 }
};

await Report.Create("User Performance")
    .From(users)                              // bind data first — T is inferred
    .AddColumn("Name", x => x.Name)
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .ToCsv("./reports/users.csv")
    .ToExcel("./reports/users.xlsx")
    .GenerateAsync();
```

### Attribute-based columns (less boilerplate)

```csharp
public class Employee
{
    [ReportColumn("Employee Name", Order = 0)]
    public string Name { get; set; } = "";

    [ReportColumn("Email", Order = 1)]
    public string Email { get; set; } = "";

    public string InternalId { get; set; } = "";  // excluded — no attribute
}

await Report.Create("Team Report")
    .From(employees)
    .AddColumnsFromAttributes()                    // discovers [ReportColumn] properties
    .ToCsv("team.csv")
    .GenerateAsync();
```

### Reusable templates

```csharp
var salesTemplate = ReportTemplate<Sale>.Define("Sales Report")
    .AddColumn("Product", x => x.Product)
    .AddColumn("Revenue", x => x.Revenue)
    .Build();

// Use with different data each time
await salesTemplate.From(marchData).ToCsv("march.csv").GenerateAsync();
await salesTemplate.From(aprilData, "April Sales").ToExcel("april.xlsx").GenerateAsync();
```

### Excel format strings (number, date, currency)

Pass an Excel format string as the third argument to `AddColumn` — it's applied to every data cell in that column. CSV exporters silently ignore it.

```csharp
await Report.Create("Financials")
    .From(orders)
    .AddColumn("Product",  x => x.Product)
    .AddColumn("Revenue",  x => x.Revenue,  "$#,##0.00")  // currency
    .AddColumn("Margin",   x => x.Margin,   "0.00%")      // percentage
    .AddColumn("Shipped",  x => x.ShippedAt,"dd/MM/yyyy") // date
    .AddColumn("Units",    x => x.Units,    "#,##0")      // thousands separator
    .ToExcel("financials.xlsx")
    .GenerateAsync();
```

Works the same way on attributes:

```csharp
public class Order
{
    [ReportColumn("Revenue", Order = 0, Format = "$#,##0.00")]
    public decimal Revenue { get; set; }

    [ReportColumn("Shipped", Order = 1, Format = "dd/MM/yyyy")]
    public DateOnly ShippedAt { get; set; }
}
```

### Culture-aware export

Both `ToCsv` and `ToExcel` accept an optional `CultureInfo` that controls number and date serialization:

```csharp
using System.Globalization;

// German locale — produces semicolon-delimited CSV (correct for de-DE)
await Report.Create("Bericht")
    .From(data)
    .AddColumn("Preis", x => x.Price)
    .ToCsv("bericht.csv", new CultureInfo("de-DE"))
    .GenerateAsync();

// Explicit invariant culture (the default when omitted)
await Report.Create("Report")
    .From(data)
    .AddColumn("Price", x => x.Price)
    .ToExcel("report.xlsx", CultureInfo.InvariantCulture)
    .GenerateAsync();
```

> **Note:** For CSV, the culture's `TextInfo.ListSeparator` sets the delimiter — European locales produce `;` instead of `,`. For Excel, the culture only affects the `ToString()` fallback for unrecognised types; use column-level format strings for locale-specific cell display.

### Multi-sheet Excel workbooks

Use `MultiSheetExcelExporter` to write several typed datasets into a single `.xlsx`, one sheet each:

```csharp
using ReportGen.Exporters;

await new MultiSheetExcelExporter("annual.xlsx")
    .AddSheet("Sales", salesData, b => b
        .AddColumn("Product",  x => x.Product)
        .AddColumn("Revenue",  x => x.Revenue,  "$#,##0.00")
        .AddColumn("Units",    x => x.Units,    "#,##0"))
    .AddSheet("Expenses", expenseData, b => b
        .AddColumn("Category", x => x.Category)
        .AddColumn("Amount",   x => x.Amount,   "$#,##0.00")
        .AddColumn("Date",     x => x.Date,     "dd/MM/yyyy"))
    .AddSheet("Headcount", staffData, b => b
        .AddColumn("Team",     x => x.Team)
        .AddColumn("Count",    x => x.Count))
    .WriteAsync(cancellationToken);
```

Each sheet can have a **different row type** — `salesData`, `expenseData`, and `staffData` above are all different `IEnumerable<T>`. Culture can be passed to the constructor and applies to all sheets:

```csharp
await new MultiSheetExcelExporter("annual.xlsx", new CultureInfo("en-GB"))
    .AddSheet(...)
    .WriteAsync();
```

> **Note:** `MultiSheetExcelExporter` has its own `WriteAsync()` and does not plug into the `.GenerateAsync()` pipeline. Use it when you need a single workbook from multiple sources; use the regular `.ToExcel()` chain for single-sheet exports.

### Export to a stream (ASP.NET downloads, memory, S3, ...)

```csharp
// In-memory — useful for email attachments, S3 uploads, tests
using var ms = new MemoryStream();
await Report.Create("Export")
    .From(data)
    .AddColumn("Name", x => x.Name)
    .ToCsv(ms)           // or .ToExcel(ms)
    .GenerateAsync();

// ASP.NET — stream directly to browser, no temp file
Response.ContentType = "text/csv";
Response.Headers["Content-Disposition"] = "attachment; filename=report.csv";
await Report.Create("Export")
    .From(data)
    .AddColumn("Name", x => x.Name)
    .ToCsv(Response.Body)
    .GenerateAsync();
```

## Packages

| Package | Description | Dependencies |
|---|---|---|
| **ReportGen.Core** | Contracts, fluent builder, templates, attribute discovery | None |
| **ReportGen.Exporters** | CSV + Excel exporters | CsvHelper, ClosedXML |

## Roadmap

### v0.1.0 — current
- [x] Core contracts and fluent builder
- [x] Attribute-based column discovery
- [x] Reusable report templates
- [x] CSV exporter — file path and stream (CsvHelper)
- [x] Excel exporter — file path and stream (ClosedXML)
- [x] Full .NET 8 type support (DateOnly, TimeOnly, Guid, short, uint, byte)
- [x] 73 tests (unit + integration)
- [x] CI pipeline
- [x] NuGet publish

### v0.2.0 — Output quality
- [x] Column-level Excel format strings (`"#,##0.00"`, `"dd/MM/yyyy"`, `"$#,##0"`, etc.)
- [x] CultureInfo support on exporters (number/date formatting per locale)
- [x] Multi-sheet workbook support (multiple `IEnumerable<T>` sources in one `.xlsx`)

### v0.3.0 — ASP.NET Core integration
- [ ] `ReportGen.AspNetCore` package — `IActionResult` helpers, `FileResult`, minimal API extensions
- [ ] Stream-to-response helpers (no temp files, direct browser download)
- [ ] `Content-Disposition` / MIME type wired automatically per format

### v0.4.0 — Report feature completeness
- [ ] Aggregate/summary rows (totals, averages, custom footer cells)
- [ ] Dynamic column selection from a registry (whitelist-based, frontend-safe)
- [ ] Column visibility / conditional inclusion at generation time

### v1.0.0 — Stable release
- [ ] PDF exporter (QuestPDF — zero-cost, .NET-native)
- [ ] Stable API commitment — no breaking changes after this point
- [ ] Full XML doc coverage on all public APIs

### v1.x — Delivery & async jobs
- [ ] Delivery abstraction (`IReportDelivery` — email, S3, Azure Blob, filesystem)
- [ ] Domain events (`ReportRequested`, `ReportGenerated`, `ReportFailed`)
- [ ] In-memory job queue (`System.Threading.Channels`)
- [ ] Email delivery (MailKit)
- [ ] Azure Service Bus / RabbitMQ adapters

## Project Structure

```text
ReportGen/
├── src/
│   ├── ReportGen.Core/          # Zero-dependency contracts & builder
│   └── ReportGen.Exporters/     # CSV + Excel implementations
├── tests/
│   └── ReportGen.Tests/         # xUnit + FluentAssertions
├── samples/
│   └── BasicUsage/              # Working console demo
├── docs/
│   ├── ARCHITECTURE.md          # Design decisions & ADRs
│   └── CONTRACT-DESIGN-GUIDE.md # Deep-dive into every contract
└── .github/workflows/ci.yml
```

## Tech Stack

- .NET 8 / C# 12
- ClosedXML (Excel)
- CsvHelper (CSV)
- xUnit + FluentAssertions

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE)
