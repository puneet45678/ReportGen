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
dotnet add package ReportGen.Core --version 0.1.0-alpha
dotnet add package ReportGen.Exporters --version 0.1.0-alpha
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

## Packages

| Package | Description | Dependencies |
|---|---|---|
| **ReportGen.Core** | Contracts, fluent builder, templates, attribute discovery | None |
| **ReportGen.Exporters** | CSV + Excel exporters | CsvHelper, ClosedXML |

## Roadmap

### v0.1.0-alpha (MVP-1) — current
- [x] Core contracts and fluent builder
- [x] Attribute-based column discovery
- [x] Reusable report templates
- [x] CSV exporter (CsvHelper)
- [x] Excel exporter (ClosedXML)
- [x] Unit tests
- [x] CI pipeline
- [ ] NuGet publish

### MVP-2
- [ ] Multi-sheet workbook support
- [ ] Delivery abstraction (IReportDelivery)
- [ ] In-memory queue + worker (IReportJobQueue)
- [ ] Domain events (ReportRequested, ReportGenerated, ReportFailed)

### v2.0
- [ ] PDF exporter
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
