# ReportGen

A modular, extensible .NET report generation library with a fluent builder API.

Current focus: CSV + Excel exports for v0.1.0-alpha, with future support for PDF, email delivery, and event-driven workers.

## Why ReportGen?

Most teams need the same report capabilities repeatedly:
- Build tabular reports from object collections
- Export to multiple formats
- Keep the API simple and strongly typed
- Extend delivery/execution later (email, queue workers)

ReportGen provides this with a clean fluent API and package-first developer experience.

## Installation (Planned for v0.1.0-alpha)

```bash
dotnet add package ReportGen.Core --prerelease
dotnet add package ReportGen.Exporters --prerelease
```

## Quickstart (Target API)

```csharp
using ReportGen.Core;
using ReportGen.Exporters;

var users = new[]
{
    new { Name = "Ava", Email = "ava@company.com", Score = 92 },
    new { Name = "Noah", Email = "noah@company.com", Score = 88 }
};

await Report.Create("User Performance")
    .AddColumn("Name", x => x.Name)
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .From(users)
    .ExportCsvAsync("./reports/users.csv")
    .ExportExcelAsync("./reports/users.xlsx");
```

Note: This is the intended public API and may evolve until the first alpha is published.

## Roadmap

### Prerequisites
- [x] Repo initialized
- [x] CI pipeline
- [x] Contributing guide + templates
- [x] Notion project tracker

### MVP-1 (v0.1.0-alpha)
- [ ] Core contracts and fluent builder
- [ ] CSV exporter
- [ ] Excel exporter (ClosedXML)
- [ ] Unit tests (target 70%+)
- [ ] NuGet prerelease publish

### MVP-2
- [ ] Delivery abstraction (IReportDelivery)
- [ ] In-memory queue + worker (IReportJobQueue)
- [ ] Domain events (ReportRequested, ReportGenerated, ReportFailed)

### v2.0
- [ ] Email delivery (MailKit)
- [ ] Azure Service Bus / RabbitMQ adapters

## Project Structure (Planned)

```text
ReportGen/
├── src/
│   ├── ReportGen.Core/
│   └── ReportGen.Exporters/
├── tests/
│   └── ReportGen.Tests/
├── samples/
│   └── BasicUsage/
└── .github/workflows/
```

## Tech Stack

- .NET 6+ / .NET 8
- C#
- ClosedXML (Excel)
- CsvHelper (CSV)
- xUnit + FluentAssertions

## Contributing

See CONTRIBUTING.md.

## License

MIT
