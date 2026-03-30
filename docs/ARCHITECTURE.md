# ReportGen — Architecture Design Document

**Version:** 0.1.0-alpha (MVP-1)
**Status:** Draft
**Last Updated:** 2026-03-30

---

## Table of Contents

1. [Architectural Vision & Goals](#1-architectural-vision--goals)
2. [Layer & Responsibility Breakdown](#2-layer--responsibility-breakdown)
3. [Core Abstractions & Contracts](#3-core-abstractions--contracts)
4. [Fluent Builder Design](#4-fluent-builder-design)
5. [Exporter Architecture](#5-exporter-architecture)
6. [MVP-2 Architecture: Delivery & Queue Abstractions](#6-mvp-2-architecture-delivery--queue-abstractions)
7. [NuGet Package Boundary Design](#7-nuget-package-boundary-design)
8. [Extensibility Map](#8-extensibility-map)
9. [Error Handling & Validation Strategy](#9-error-handling--validation-strategy)
10. [Testing Architecture](#10-testing-architecture)
11. [Integration Guidance for Consumers](#11-integration-guidance-for-consumers)
12. [Decision Log (ADRs)](#12-decision-log-adrs)

---

## 1. Architectural Vision & Goals

### 1.1 Non-Negotiable Design Principles

| # | Principle | Concrete Meaning |
|---|-----------|-----------------|
| 1 | **Dependency Inversion** | Core defines abstractions. Every concrete implementation (CSV, Excel, Email, Queue) depends on Core — never the reverse. A consumer who only needs CSV never pulls in ClosedXML. |
| 2 | **Type Safety Over Convenience** | Column definitions use `Func<T, object?>` lambdas resolved at compile time. No reflection, no magic strings for property access. If a column accessor is wrong, the compiler catches it — not a runtime exception in production. |
| 3 | **Async-First, CancellationToken Everywhere** | Every I/O operation accepts `CancellationToken`. Report generation against large datasets can be cancelled. This is table stakes for a library consumed in ASP.NET Core request pipelines. |
| 4 | **Immutable Data, Mutable Builder** | The builder collects configuration mutably (standard pattern — `StringBuilder`, `IHostBuilder`). Once `.GenerateAsync()` is called, the builder produces an immutable `ReportDefinition<T>` snapshot that exporters receive. Exporters never see or mutate builder state. |
| 5 | **Zero Forced Dependencies** | `ReportGen.Core` has zero third-party NuGet dependencies. Third-party libraries (ClosedXML, CsvHelper) live only in `ReportGen.Exporters`. Consumers writing custom exporters depend only on Core. |
| 6 | **Extension via Composition, Not Inheritance** | Custom exporters implement `IReportExporter`. Custom delivery implements `IReportDelivery`. No base classes to inherit, no virtual methods to override. Interfaces are the only extension contracts. |

### 1.2 What "Extensible" Means Concretely

"Extensible" is not a vague aspiration. It means the following scenarios work **without modifying ReportGen source code**:

1. **Add a new export format** — Implement `IReportExporter`, register it with `.AddExporter()` or write a one-line extension method. Done.
2. **Add a new delivery channel** (MVP-2) — Implement `IReportDelivery`. No changes to the generation pipeline.
3. **Add a custom column formatter** — Pass a `Func<T, object?>` that does whatever transformation is needed. No formatter interface required.
4. **Swap data source** — Any `IEnumerable<T>` works: in-memory lists, EF Core queries, Dapper results, CSV readers.
5. **Integrate with DI** — All contracts are interfaces. Registration in `IServiceCollection` is trivial. No static singletons blocking testability.

### 1.3 What Bad Architecture Costs NuGet Consumers

| Poor Design Decision | Consumer Impact |
|----------------------|----------------|
| Core depends on ClosedXML | Every consumer pays the dependency tax even if they only want CSV |
| Builder exposes mutable state to exporters | Race conditions in concurrent exports; unpredictable behavior |
| No `CancellationToken` support | Consumers can't cancel long-running exports in ASP.NET Core; request timeouts cause orphaned threads |
| Sealed exporter pipeline | Consumers fork the repo instead of extending it; your NuGet download count flatlines |
| String-based column names via reflection | Runtime `MissingMemberException` instead of compile-time errors; refactoring tools can't track usage |
| Single monolithic NuGet package | Version bumps for an Excel bug force everyone to update, even CSV-only users |

---

## 2. Layer & Responsibility Breakdown

### 2.1 Package Architecture

```
┌──────────────────────────────────────────────────────┐
│                  Consumer Application                 │
│  (ASP.NET Core API, Console App, Worker Service)     │
└──────┬──────────────────────────────────┬────────────┘
       │ references                       │ references
       ▼                                  ▼
┌─────────────────────┐    ┌─────────────────────────────┐
│  ReportGen.Core     │◄───│  ReportGen.Exporters        │
│                     │    │                              │
│  • IReportExporter  │    │  • CsvExporter (CsvHelper)  │
│  • IReportBuilder<T>│    │  • ExcelExporter (ClosedXML)│
│  • ReportDefinition │    │  • Extension methods:        │
│  • ColumnDefinition │    │    .ToCsv(), .ToExcel()     │
│  • Report (entry)   │    │                              │
│                     │    │  Dependencies:               │
│  Dependencies:      │    │  → ReportGen.Core            │
│  → (none)           │    │  → CsvHelper                 │
│                     │    │  → ClosedXML                 │
└─────────────────────┘    └─────────────────────────────┘
```

**Dependency Rule:** Arrows point inward. `Exporters → Core`. Never `Core → Exporters`. The consumer references both packages, but Core is unaware that Exporters exists.

### 2.2 What Lives Where — and Why

#### ReportGen.Core

| Component | Justification |
|-----------|---------------|
| `Report` static class (entry point) | Must be in Core so the fluent chain starts without requiring Exporters |
| `IReportBuilder<T>` interface | Defines the fluent contract; exporters extend it via extension methods |
| `IReportExporter` interface | The contract exporters fulfill; must be in Core so the builder can accept exporters without knowing their concrete types |
| `ReportDefinition<T>` record | The immutable data model passed from builder to exporter; shared vocabulary between layers |
| `ColumnDefinition<T>` record | Value object representing one column; owned by Core because it's part of the definition model |
| `ReportBuilder<T>` class (internal) | The concrete builder implementation; `internal` because consumers interact through the interface, not the class |
| `IReportBuilderSource` interface | The untyped pre-`.From()` builder stage; see Section 4 for the fluent phase design |

#### ReportGen.Exporters

| Component | Justification |
|-----------|---------------|
| `CsvExporter : IReportExporter` | Concrete implementation; depends on CsvHelper; consumers who don't want CSV never see this |
| `ExcelExporter : IReportExporter` | Concrete implementation; depends on ClosedXML |
| `ReportBuilderExtensions` static class | Extension methods (`.ToCsv()`, `.ToExcel()`) that provide fluent sugar on top of `.AddExporter()` |

#### Why This Split?

The split exists for **dependency isolation**. Consider two consumers:

1. **Team A** wants CSV only. They reference `ReportGen.Core` + write a trivial CSV exporter (or reference `ReportGen.Exporters` and accept the ClosedXML transitive dependency they don't use).
2. **Team B** writes a PDF exporter. They reference only `ReportGen.Core`, implement `IReportExporter`, and never touch `ReportGen.Exporters`.

If Core contained ClosedXML references, Team B would be forced to pull ClosedXML into their project for no reason. This is the concrete cost of violating dependency inversion in a NuGet context.

**Future consideration (v1.0+):** If the package grows, split `ReportGen.Exporters` further into `ReportGen.Exporters.Csv` and `ReportGen.Exporters.Excel` so consumers can pick individual formats. For MVP-1, a single Exporters package is acceptable — the format count is small and the audience is early adopters.

### 2.3 Dependency Inversion Applied

```
Traditional (wrong):
  Builder → CsvExporter (concrete)
  Builder → ExcelExporter (concrete)
  Result: Builder can't exist without both exporters

Inverted (correct):
  Builder → IReportExporter (abstraction, in Core)
  CsvExporter → IReportExporter (implements, in Exporters)
  ExcelExporter → IReportExporter (implements, in Exporters)
  Result: Builder doesn't know any concrete exporter exists
```

The builder's `AddExporter(IReportExporter)` method accepts any implementation. The builder produces a `ReportDefinition<T>` and passes it to each registered exporter. The builder doesn't know — and must never know — whether the exporter writes CSV, Excel, PDF, or sends a carrier pigeon.

---

## 3. Core Abstractions & Contracts

### 3.1 ColumnDefinition\<T\>

```csharp
namespace ReportGen.Core;

/// <summary>
/// Immutable definition of a single report column.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed record ColumnDefinition<T>(
    string Header,
    Func<T, object?> Accessor,
    int Order);
```

**Design Rationale:**

- **`record`** — Value semantics, immutable by default, structural equality for free. A column definition is a value object: two definitions with the same header and accessor are logically equivalent.
- **`Func<T, object?>`** — Returns `object?` rather than a specific type because report cells are heterogeneous (strings, ints, dates, nulls). The exporter is responsible for formatting. Returning `object?` avoids forcing consumers to specify a cell type generic parameter for every column, which would make the API unbearable: `.AddColumn<string>("Name", x => x.Name)`.
- **`int Order`** — Explicit column ordering. The builder sets this based on the order `.AddColumn()` is called. This is critical because `IReadOnlyList<ColumnDefinition<T>>` preserves insertion order, but having an explicit `Order` property makes sorting deterministic and debuggable.
- **`sealed`** — Nobody should inherit from this. It's a data carrier, not a polymorphic type.

**What happens if designed poorly:**

- If `ColumnDefinition` were a mutable class, an exporter could accidentally mutate column headers during rendering, corrupting subsequent exports in a multi-export pipeline.
- If the accessor returned `string` instead of `object?`, consumers would be forced to `.ToString()` everything at the call site, losing numeric formatting control in Excel.

### 3.2 ReportDefinition\<T\>

```csharp
namespace ReportGen.Core;

/// <summary>
/// Immutable snapshot of a fully configured report, ready for export.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed record ReportDefinition<T>
{
    public required string Title { get; init; }
    public required IReadOnlyList<ColumnDefinition<T>> Columns { get; init; }
    public required IReadOnlyList<T> Data { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

**Design Rationale:**

- **`IReadOnlyList<T>` for Data** — The data is materialized (not `IEnumerable<T>`) by the time it reaches the exporter. This is a deliberate decision:
  - Multiple exporters iterate over the same data. With `IEnumerable<T>`, the second exporter would get an empty sequence if the source is a single-use database cursor.
  - Materializing once makes memory cost explicit and predictable.
  - The materialization happens in `GenerateAsync()`, not in the exporter. The exporter is a pure renderer — it doesn't manage data lifecycle.
- **`GeneratedAtUtc`** — Metadata that exporters can embed (e.g., "Generated on 2026-03-30" in an Excel footer). Defaulted via `init` so the builder doesn't need to set it explicitly.
- **`required` keyword** — Prevents construction of an incomplete definition. If you forget `Title` or `Columns`, the compiler rejects it.

**What happens if designed poorly:**

- If `Data` were `IEnumerable<T>`, an exporter writing to a slow stream could hold open a database connection for minutes. Materializing upfront bounds the data lifecycle.
- If this were a class with public setters, an exporter could silently mutate the title. The next exporter in the chain would write the wrong name. This is the kind of bug that takes hours to diagnose.

### 3.3 IReportExporter

```csharp
namespace ReportGen.Core;

/// <summary>
/// Contract for all report format exporters.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Exports the report definition to the target format/destination.
    /// </summary>
    Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default);
}
```

**Design Rationale:**

- **Generic method, not generic interface** — The interface is `IReportExporter`, not `IReportExporter<T>`. The generic parameter is on the method. This is critical:
  - A single `CsvExporter` instance can export `ReportDefinition<User>` and `ReportDefinition<Order>` without being constructed separately for each type.
  - DI registration is simpler: `services.AddSingleton<IReportExporter, CsvExporter>()` — one registration, works for all types.
  - If the interface were generic (`IReportExporter<T>`), you'd need `IReportExporter<User>`, `IReportExporter<Order>`, etc. Registration and resolution become a generic type puzzle.
- **`Task` return type** — Exporters perform I/O (file writes, stream writes). Async is non-negotiable. Returns `Task` (not `Task<T>`) because the output is a side effect (file written), not a value.
- **`CancellationToken`** — Every async I/O method must accept this. Non-negotiable for use in ASP.NET Core where request cancellation is routine.

**What happens if designed poorly:**

- If the interface were synchronous (`void Export<T>(...)`), consumers in async pipelines would be forced to wrap calls in `Task.Run()`, wasting thread pool threads.
- If the interface returned `Stream` instead of writing to a destination, the caller would be responsible for disposing the stream and writing it somewhere. This leaks resource management responsibility across layer boundaries.

**Alternative considered — `ExportAsync` returning `Task<Stream>`:**

Rejected because it shifts the "where to write" decision to the caller. The exporter should own the full write lifecycle. For cases where a consumer wants an in-memory result (e.g., return a CSV as an HTTP response), a separate `IReportExporter` implementation can write to a `MemoryStream` provided via constructor. The interface stays clean.

### 3.4 IReportBuilder\<T\> and IReportBuilderSource

```csharp
namespace ReportGen.Core;

/// <summary>
/// Pre-data builder stage. Holds report metadata before data type T is known.
/// </summary>
public interface IReportBuilderSource
{
    /// <summary>
    /// Binds data to the report and transitions to the typed builder.
    /// </summary>
    IReportBuilder<T> From<T>(IEnumerable<T> data);
}

/// <summary>
/// Typed fluent builder for constructing and executing a report.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public interface IReportBuilder<T>
{
    /// <summary>
    /// Adds a column definition to the report.
    /// </summary>
    IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor);

    /// <summary>
    /// Registers an exporter to be executed during generation.
    /// </summary>
    IReportBuilder<T> AddExporter(IReportExporter exporter);

    /// <summary>
    /// Builds the immutable report definition without executing exports.
    /// </summary>
    ReportDefinition<T> Build();

    /// <summary>
    /// Builds the report definition and executes all registered exporters.
    /// </summary>
    Task GenerateAsync(CancellationToken cancellationToken = default);
}
```

**Design Rationale:**

- **Two-phase builder** — `IReportBuilderSource` exists because `T` is unknown until `.From()` is called. See Section 4 for the full explanation of why the README's target API requires correction.
- **`AddExporter` on the interface** — This is the primitive extension point. `.ToCsv()` and `.ToExcel()` are sugar built on top of this. Any consumer can call `AddExporter(new MyPdfExporter(...))` without extension methods.
- **`Build()` separate from `GenerateAsync()`** — `Build()` produces the `ReportDefinition<T>` without executing side effects. This is essential for testing: you can assert on column headers, data counts, etc. without writing files. `GenerateAsync()` calls `Build()` internally and then runs all exporters.
- **Returns `IReportBuilder<T>` (fluent)** — Every configuration method returns the builder so calls can be chained. This is the standard fluent pattern.

### 3.5 Report Static Entry Point

```csharp
namespace ReportGen.Core;

/// <summary>
/// Entry point for the fluent report builder API.
/// </summary>
public static class Report
{
    /// <summary>
    /// Creates a new report builder with the given title.
    /// </summary>
    public static IReportBuilderSource Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new ReportBuilderSource(title);
    }
}
```

- **`static class`** — No instantiation needed. Pure factory method. This is the sole entry point for consumers.
- **Input validation at the boundary** — `ThrowIfNullOrWhiteSpace` is the correct guard. A report with a blank title is always a bug.
- **Returns `IReportBuilderSource`** (interface, not concrete type) — Consumer code programs against the abstraction. The concrete `ReportBuilderSource` is `internal`.

### 3.6 Types Summary — What Crosses Layer Boundaries

```
Builder (Core, internal)
    ↓ produces
ReportDefinition<T> (Core, public)  ← this is the cross-boundary contract
    ↓ consumed by
IReportExporter.ExportAsync<T>()
    ↓ implemented by
CsvExporter / ExcelExporter (Exporters, public)
```

The **only types that cross the Core → Exporter boundary** are:
- `ReportDefinition<T>` — the immutable snapshot
- `ColumnDefinition<T>` — contained within the definition
- `CancellationToken` — BCL type

No builder state, no configuration objects, no mutable references cross this boundary.

---

## 4. Fluent Builder Design

### 4.1 Critical API Correction

The README's target API has a C# compilation problem:

```csharp
// README target — WILL NOT COMPILE
await Report.Create("User Performance")
    .AddColumn("Name", x => x.Name)      // ← compiler error: type of 'x' is unknown
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .From(users)
    .ExportCsvAsync("./reports/users.csv")
    .ExportExcelAsync("./reports/users.xlsx");
```

**Why it fails:** `.AddColumn("Name", x => x.Name)` is called before `.From(users)`. The lambda `x => x.Name` requires the compiler to know the type of `x`. Without `.From()` binding the type parameter `T`, the compiler has no way to infer what `x` is. This is a fundamental C# type inference limitation — generic type parameters must be resolvable at each call site.

**Corrected API:**

```csharp
await Report.Create("User Performance")
    .From(users)                           // T inferred as the element type of users
    .AddColumn("Name", x => x.Name)       // x is now typed — compiles
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .ToCsv("./reports/users.csv")          // registers CsvExporter (extension method from Exporters)
    .ToExcel("./reports/users.xlsx")       // registers ExcelExporter (extension method from Exporters)
    .GenerateAsync();                      // terminal: builds definition, runs all exporters
```

This reads naturally: *"Create a report called 'User Performance', from this data, with these columns, to CSV and Excel, generate."*

### 4.2 Fluent Phase Transitions

The builder has two phases, enforced by the type system:

```
Phase 1: IReportBuilderSource              Phase 2: IReportBuilder<T>
┌────────────────────────┐                 ┌──────────────────────────────────┐
│ Report.Create("Title") │ ── .From() ──▸ │ .AddColumn("H", x => x.Prop)    │
│                        │                 │ .AddExporter(exporter)           │
│ Available methods:     │                 │ .ToCsv("path")  [extension]     │
│  • .From<T>(data)      │                 │ .ToExcel("path") [extension]     │
│                        │                 │ .Build()         [terminal]      │
│                        │                 │ .GenerateAsync() [terminal]      │
└────────────────────────┘                 └──────────────────────────────────┘
```

**Why two phases?** The type system prevents you from calling `.AddColumn()` before `.From()`. This is compile-time safety — you physically cannot construct an invalid call sequence. This is better than a runtime check like `if (data == null) throw`.

### 4.3 Internal Builder Implementation

```csharp
namespace ReportGen.Core.Internal;

internal sealed class ReportBuilderSource : IReportBuilderSource
{
    private readonly string _title;

    internal ReportBuilderSource(string title) => _title = title;

    public IReportBuilder<T> From<T>(IEnumerable<T> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ReportBuilder<T>(_title, data);
    }
}

internal sealed class ReportBuilder<T> : IReportBuilder<T>
{
    private readonly string _title;
    private readonly IEnumerable<T> _data;
    private readonly List<ColumnDefinition<T>> _columns = [];
    private readonly List<IReportExporter> _exporters = [];
    private int _columnOrder;

    internal ReportBuilder(string title, IEnumerable<T> data)
    {
        _title = title;
        _data = data;
    }

    public IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentNullException.ThrowIfNull(accessor);
        _columns.Add(new ColumnDefinition<T>(header, accessor, _columnOrder++));
        return this;
    }

    public IReportBuilder<T> AddExporter(IReportExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        _exporters.Add(exporter);
        return this;
    }

    public ReportDefinition<T> Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be defined.");

        return new ReportDefinition<T>
        {
            Title = _title,
            Columns = _columns.OrderBy(c => c.Order).ToList().AsReadOnly(),
            Data = _data.ToList().AsReadOnly()   // materialize once
        };
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (_exporters.Count == 0)
            throw new InvalidOperationException("At least one exporter must be registered.");

        var definition = Build();

        foreach (var exporter in _exporters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await exporter.ExportAsync(definition, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

### 4.4 Key Design Decisions in the Builder

#### Mutable builder is intentional

The builder mutates internal state (`_columns`, `_exporters`). This is the standard .NET pattern — `StringBuilder`, `ConfigurationBuilder`, `HostBuilder` all work this way. An immutable builder (returning a new instance from every method) would allocate more objects for no benefit, since builders are not shared across threads.

**Thread safety:** The builder is **not thread-safe**. This is by convention for builders in .NET. If someone needs to build from multiple threads, they should synchronize externally. Documenting this in XML docs is sufficient.

#### Data materialization happens in Build()

`_data.ToList().AsReadOnly()` in `Build()` materializes the `IEnumerable<T>` into a concrete `IReadOnlyList<T>`. This happens exactly once, even with multiple exporters:

```csharp
var definition = Build();              // data materialized here
await exporter1.ExportAsync(definition, ct);  // reads materialized list
await exporter2.ExportAsync(definition, ct);  // reads same list — no re-enumeration
```

**Tradeoff acknowledged:** Materialization copies all data into memory. For very large datasets (millions of rows), this is a concern. Future versions could introduce a streaming API (`IAsyncEnumerable<T>`) as an opt-in alternative. For MVP-1, materialization is the safe default — it prevents subtle bugs with disposed database connections or single-use enumerators.

#### Multiple exports run sequentially

Exporters run in a `foreach` loop, not in parallel (`Task.WhenAll`). Reasons:

1. **Predictable resource usage** — Two exporters writing to disk simultaneously could cause I/O contention.
2. **Simple error handling** — If the first exporter fails, the second never starts. The exception propagates cleanly.
3. **CancellationToken is respected between exports** — Checked before each exporter starts.

If a consumer needs parallel exports, they can call `Build()` to get the definition and run exporters themselves with `Task.WhenAll`. The primitive is exposed; the default is safe.

#### `.ExportCsvAsync()` chaining problem

The README showed:
```csharp
.ExportCsvAsync("./reports/users.csv")
.ExportExcelAsync("./reports/users.xlsx");
```

This implies chaining async calls. If `ExportCsvAsync` returns `Task`, you can't call `.ExportExcelAsync()` on a `Task`. You'd need either:

- `await (await report.ExportCsvAsync(...)).ExportExcelAsync(...)` — ugly, confusing
- Extension methods on `Task<IReportBuilder<T>>` — clever but hostile to debuggers and stack traces
- Custom awaitables — massive overengineering for this use case

**Our solution:** `.ToCsv()` and `.ToExcel()` are synchronous configuration methods that *register* exporters. `.GenerateAsync()` is the single async terminal operation. This is clean, debuggable, and unsurprising.

```csharp
// Clean: configure synchronously, execute once
await Report.Create("Sales Report")
    .From(orders)
    .AddColumn("Product", x => x.Product)
    .AddColumn("Revenue", x => x.Revenue)
    .ToCsv("./reports/sales.csv")
    .ToExcel("./reports/sales.xlsx")
    .GenerateAsync();

// Also supported: single export
await Report.Create("Quick Export")
    .From(items)
    .AddColumn("Name", x => x.Name)
    .ToCsv("./output.csv")
    .GenerateAsync();
```

---

## 5. Exporter Architecture

### 5.1 CsvExporter

```csharp
namespace ReportGen.Exporters;

public sealed class CsvExporter : IReportExporter
{
    private readonly string _filePath;

    public CsvExporter(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var writer = new StreamWriter(_filePath, append: false, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header row
        foreach (var column in report.Columns)
        {
            csv.WriteField(column.Header);
        }
        await csv.NextRecordAsync();

        // Write data rows
        foreach (var row in report.Data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var column in report.Columns)
            {
                csv.WriteField(column.Accessor(row));
            }
            await csv.NextRecordAsync();
        }

        await csv.FlushAsync();
    }
}
```

### 5.2 ExcelExporter

```csharp
namespace ReportGen.Exporters;

public sealed class ExcelExporter : IReportExporter
{
    private readonly string _filePath;

    public ExcelExporter(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(report.Title));

        // Write header row
        for (var col = 0; col < report.Columns.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = report.Columns[col].Header;
            cell.Style.Font.Bold = true;
        }

        // Write data rows
        for (var rowIdx = 0; rowIdx < report.Data.Count; rowIdx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = report.Data[rowIdx];
            for (var col = 0; col < report.Columns.Count; col++)
            {
                var value = report.Columns[col].Accessor(row);
                var cell = worksheet.Cell(rowIdx + 2, col + 1);
                SetCellValue(cell, value);
            }
        }

        worksheet.Columns().AdjustToContents();

        // ClosedXML SaveAs is synchronous — offload to avoid blocking the caller
        await Task.Run(() => workbook.SaveAs(_filePath), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string SanitizeSheetName(string title)
    {
        // Excel sheet names: max 31 chars, no []:*?/\
        var sanitized = title.Length > 31 ? title[..31] : title;
        foreach (var c in new[] { '[', ']', ':', '*', '?', '/', '\\' })
            sanitized = sanitized.Replace(c, '_');
        return sanitized;
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        cell.Value = value switch
        {
            null => Blank.Value,
            string s => s,
            int i => i,
            long l => l,
            double d => d,
            decimal m => m,
            float f => f,
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            bool b => b,
            _ => value.ToString() ?? string.Empty
        };
    }
}
```

### 5.3 Rendering Pipeline Inside an Exporter

Every exporter follows the same internal pipeline:

```
ReportDefinition<T>
    │
    ▼
1. Resolve Output Target
   (create directory, open file/stream)
    │
    ▼
2. Write Header Row
   foreach column in definition.Columns → write column.Header
    │
    ▼
3. Write Data Rows
   foreach row in definition.Data →
       foreach column in definition.Columns →
           column.Accessor(row) → write cell value
    │
    ▼
4. Finalize
   (flush buffers, save workbook, close streams)
```

This pipeline is the same for CSV, Excel, PDF, or any format. The difference is the write mechanism. This uniformity is not accidental — the `ReportDefinition<T>` is designed to be a format-agnostic data structure that any renderer can consume.

### 5.4 Decoupling Exporters from the Builder

Exporters receive `ReportDefinition<T>` — never `IReportBuilder<T>`. The exporter has no reference to:
- Builder state
- Other exporters
- The configuration pipeline

This means:
- You can unit test an exporter with a hand-crafted `ReportDefinition<T>` — no builder needed.
- Exporters can be shared across different builder implementations.
- Exporter bugs are isolated and reproducible.

### 5.5 Extension Point: Custom Exporters

A consumer creating a JSON exporter:

```csharp
// In the consumer's project — references only ReportGen.Core
public sealed class JsonExporter : IReportExporter
{
    private readonly string _filePath;

    public JsonExporter(string filePath) => _filePath = filePath;

    public async Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default)
    {
        var rows = report.Data.Select(row =>
            report.Columns.ToDictionary(c => c.Header, c => c.Accessor(row)));

        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }
}

// Optional fluent extension
public static class JsonExporterExtensions
{
    public static IReportBuilder<T> ToJson<T>(this IReportBuilder<T> builder, string filePath)
        => builder.AddExporter(new JsonExporter(filePath));
}
```

**Usage:**

```csharp
await Report.Create("API Data")
    .From(records)
    .AddColumn("Id", x => x.Id)
    .AddColumn("Value", x => x.Value)
    .ToJson("./output/data.json")    // consumer's extension method
    .ToCsv("./output/data.csv")     // built-in from Exporters
    .GenerateAsync();
```

Notice: the consumer's `ToJson` sits alongside the built-in `ToCsv` in the fluent chain. No modification to ReportGen. No Pull Request. No fork. This is what "extensible" means.

### 5.6 Stream-Based Exporter Overload (Planned Enhancement)

For scenarios like ASP.NET Core responses or Azure Blob uploads, exporters should also support writing to a `Stream`:

```csharp
// Future: IReportExporter could evolve to support streams via a second interface
public interface IStreamReportExporter : IReportExporter
{
    Task ExportAsync<T>(ReportDefinition<T> report, Stream output, CancellationToken cancellationToken = default);
}
```

This is **not** in MVP-1. The file-path-based exporter is sufficient for the alpha. The `IStreamReportExporter` is noted here to ensure we don't paint ourselves into a corner — the current design accommodates this cleanly because adding an interface doesn't break existing exporters.

---

## 6. MVP-2 Architecture: Delivery & Queue Abstractions

### 6.1 Design Principle: MVP-2 Must Not Pollute MVP-1

MVP-2 introduces job queuing, delivery, and domain events. These abstractions will live in a new package:

```
ReportGen.Core                  ← unchanged, no new types for MVP-2 here
ReportGen.Exporters             ← unchanged
ReportGen.Delivery              ← NEW: delivery + queue abstractions + events
ReportGen.Delivery.InMemory     ← NEW: in-memory implementation
```

**Rule:** `ReportGen.Core` gains zero types for MVP-2. The delivery package references Core, not the other way around. A consumer on MVP-1 who doesn't need delivery never sees these types.

### 6.2 IReportDelivery

```csharp
namespace ReportGen.Delivery;

/// <summary>
/// Abstracts where/how a generated report is delivered.
/// </summary>
public interface IReportDelivery
{
    /// <summary>
    /// Delivers a generated report to its destination.
    /// </summary>
    /// <param name="reportStream">The rendered report content.</param>
    /// <param name="metadata">Contextual information about the report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeliverAsync(Stream reportStream, ReportMetadata metadata, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata describing a generated report for delivery routing.
/// </summary>
public sealed record ReportMetadata
{
    public required string Title { get; init; }
    public required string Format { get; init; }        // "csv", "xlsx", etc.
    public required string FileName { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
```

**What does `IReportDelivery` abstract?**

It abstracts the *destination* of a generated report, decoupled from format:
- **`EmailDelivery`** — sends the stream as an attachment via SMTP (MailKit)
- **`BlobStorageDelivery`** — uploads to Azure Blob Storage
- **`FileSystemDelivery`** — saves to a local/network path
- **`HttpDelivery`** — POSTs to a webhook endpoint

The delivery layer receives a `Stream` (format-agnostic binary content) and `ReportMetadata` (routing information). It does not know or care whether the stream is CSV, Excel, or PDF.

**Why `Stream` and not `byte[]`?**

- `Stream` supports large reports without loading the entire content into memory.
- `Stream` is the natural input for `SmtpClient.SendAsync`, `BlobClient.UploadAsync`, and `HttpContent`.
- For small reports, a `MemoryStream` works fine.

### 6.3 IReportJobQueue

```csharp
namespace ReportGen.Delivery;

/// <summary>
/// Queue for background report generation jobs.
/// </summary>
public interface IReportJobQueue
{
    Task EnqueueAsync(ReportJob job, CancellationToken cancellationToken = default);
}

/// <summary>
/// A deferred report generation request.
/// </summary>
public sealed record ReportJob
{
    public required string JobId { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// Factory that produces the ReportDefinition when the job executes.
    /// This defers data access to execution time (not enqueue time).
    /// </summary>
    public required Func<CancellationToken, Task<ExportableReport>> BuildReportAsync { get; init; }

    /// <summary>
    /// Delivery targets for the generated report.
    /// </summary>
    public required IReadOnlyList<IReportDelivery> Deliveries { get; init; }
}

/// <summary>
/// A report ready for export, produced by a job's build function.
/// </summary>
public sealed record ExportableReport(Stream Content, ReportMetadata Metadata);
```

**How IReportJobQueue interacts with IReportDelivery:**

```
1. Consumer enqueues: queue.EnqueueAsync(job)
2. Worker dequeues job
3. Worker calls job.BuildReportAsync(ct) → gets ExportableReport (Stream + Metadata)
4. Worker iterates job.Deliveries → calls delivery.DeliverAsync(stream, metadata, ct) for each
5. Worker publishes domain event (ReportGenerated or ReportFailed)
```

**Why `Func<CancellationToken, Task<ExportableReport>>` instead of `ReportDefinition<T>`?**

`ReportDefinition<T>` is generic — it can't be stored in a non-generic queue without type erasure. The `Func` factory defers both data access and rendering to execution time. The consumer's enqueue code captures the builder in a closure:

```csharp
var builder = Report.Create("Monthly Sales")
    .From(GetSalesData)
    .AddColumn("Product", x => x.Product)
    .AddColumn("Revenue", x => x.Revenue);

await queue.EnqueueAsync(new ReportJob
{
    JobId = Guid.NewGuid().ToString(),
    Title = "Monthly Sales",
    BuildReportAsync = async ct =>
    {
        var definition = builder.Build();
        using var stream = new MemoryStream();
        var exporter = new CsvExporter(stream);    // stream-based overload
        await exporter.ExportAsync(definition, ct);
        stream.Position = 0;
        return new ExportableReport(stream, new ReportMetadata
        {
            Title = definition.Title,
            Format = "csv",
            FileName = "monthly-sales.csv",
            GeneratedAtUtc = definition.GeneratedAtUtc
        });
    },
    Deliveries = [new EmailDelivery("finance@company.com")]
});
```

### 6.4 Domain Events

```csharp
namespace ReportGen.Delivery.Events;

/// <summary>
/// Base type for all report lifecycle events.
/// </summary>
public abstract record ReportEvent
{
    public required string JobId { get; init; }
    public required string ReportTitle { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ReportRequested : ReportEvent;

public sealed record ReportGenerated : ReportEvent
{
    public required TimeSpan Duration { get; init; }
}

public sealed record ReportFailed : ReportEvent
{
    public required string ErrorMessage { get; init; }
    public required string? StackTrace { get; init; }
}
```

**Event dispatch contract:**

```csharp
namespace ReportGen.Delivery.Events;

/// <summary>
/// Dispatches report lifecycle events to registered handlers.
/// </summary>
public interface IReportEventDispatcher
{
    Task DispatchAsync(ReportEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a specific report event type.
/// </summary>
public interface IReportEventHandler<in TEvent> where TEvent : ReportEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
```

### 6.5 Why Not MediatR?

**Decision: Custom lightweight event dispatch. Not MediatR.**

| Factor | MediatR | Custom IReportEventDispatcher |
|--------|---------|-------------------------------|
| Dependency | Forces MediatR + Microsoft.Extensions.DI on every consumer | Zero external dependencies |
| Learning curve | Consumers must understand MediatR pipeline, behaviors, etc. | Single interface, obvious semantics |
| Overhead | Full pipeline with behaviors, pre/post processors | Direct dispatch — handler list iteration |
| Testability | Must mock `IMediator` or set up full pipeline | Mock `IReportEventDispatcher` with one method |
| Library fit | MediatR is an application-level framework; libraries shouldn't force it | Library provides the primitive; consumers can bridge to MediatR if they want |

The library provides `IReportEventDispatcher` and `IReportEventHandler<T>`. A consumer already using MediatR can write a trivial bridge:

```csharp
// Consumer's bridge — not in ReportGen
public class MediatRReportEventBridge : IReportEventHandler<ReportGenerated>
{
    private readonly IMediator _mediator;
    public MediatRReportEventBridge(IMediator mediator) => _mediator = mediator;

    public Task HandleAsync(ReportGenerated @event, CancellationToken ct)
        => _mediator.Publish(@event, ct);  // bridge to MediatR notification
}
```

**Alternative considered — raw C# `event` keyword:**

Rejected. C# events are synchronous, don't support `async` handlers natively, have unintuitive multicast delegate exception behavior (if one handler throws, remaining handlers don't execute), and can't be dependency-injected. They're fine for UI frameworks, wrong for library-level async notifications.

### 6.6 In-Memory Queue Implementation (MVP-2 Default)

```csharp
namespace ReportGen.Delivery.InMemory;

/// <summary>
/// Channel-based in-memory job queue for single-process deployments.
/// </summary>
public sealed class InMemoryReportJobQueue : IReportJobQueue
{
    private readonly Channel<ReportJob> _channel;

    public InMemoryReportJobQueue(int capacity = 100)
    {
        _channel = Channel.CreateBounded<ReportJob>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task EnqueueAsync(ReportJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _channel.Writer.WriteAsync(job, cancellationToken).ConfigureAwait(false);
    }

    // Used by the background worker to consume jobs
    public ChannelReader<ReportJob> Reader => _channel.Reader;
}
```

Uses `System.Threading.Channels` — zero external dependencies, high-performance, backpressure-aware. The `BoundedChannelOptions` with `Wait` mode prevents unbounded memory growth if jobs are enqueued faster than they're processed.

The in-memory queue is a default for development and small deployments. In v2.0, `RabbitMqReportJobQueue` and `ServiceBusReportJobQueue` implement the same `IReportJobQueue` interface for distributed scenarios.

### 6.7 How MVP-2 Prepares for v2.0 Without Contaminating MVP-1

```
MVP-1 Consumer                        MVP-2 Consumer
┌──────────────────┐                  ┌──────────────────────────────────┐
│ ReportGen.Core   │                  │ ReportGen.Core                   │
│ ReportGen.       │                  │ ReportGen.Exporters              │
│   Exporters      │                  │ ReportGen.Delivery               │
│                  │                  │ ReportGen.Delivery.InMemory      │
│ No queue, no     │                  │                                  │
│ events, no       │                  │ Queue + Delivery + Events        │
│ delivery.        │                  │ available.                       │
└──────────────────┘                  └──────────────────────────────────┘
```

**Separation guarantees:**
- MVP-1 packages gain zero new types or dependencies when MVP-2 ships.
- `ReportGen.Core.csproj` does not reference `ReportGen.Delivery`.
- MVP-1 consumers continue to work without changes after MVP-2 is published.
- The delivery package references Core (for `ReportDefinition<T>` if needed) but Core never references delivery.

---

## 7. Appendix: Decision Log

This log captures architectural decisions with status and rationale for future reference.

| # | Decision | Status | Rationale |
|---|----------|--------|-----------|
| ADR-001 | `.From()` before `.AddColumn()` in fluent chain | **Accepted** | C# type inference requires `T` to be bound before lambdas reference its members. Placing `.From()` first enables inference without requiring `Report.Create<T>()`. |
| ADR-002 | Mutable builder, immutable definition | **Accepted** | Standard .NET builder pattern. Minimizes allocations during configuration. `ReportDefinition<T>` is immutable to prevent exporter-side mutation. |
| ADR-003 | `IReportExporter` has generic method, not generic interface | **Accepted** | Simplifies DI registration and allows one exporter instance to handle multiple `T` types. |
| ADR-004 | Materialize `IEnumerable<T>` in `Build()` | **Accepted** | Prevents multiple enumeration bugs with database cursors. Explicit memory cost is preferable to subtle data lifecycle issues. |
| ADR-005 | Sequential exporter execution (not parallel) | **Accepted** | Safe default. Parallel execution available via manual `Build()` + `Task.WhenAll`. |
| ADR-006 | Custom event dispatch over MediatR | **Accepted** | Libraries should not force application-level framework dependencies on consumers. Bridging to MediatR is trivial for consumers who want it. |
| ADR-007 | `System.Threading.Channels` for in-memory queue | **Accepted** | BCL type with zero dependencies, high performance, built-in backpressure. Natural fit for `IHostedService` workers. |
| ADR-008 | Separate NuGet packages per concern | **Accepted** | Dependency isolation. CSV-only consumers never pull ClosedXML. Delivery consumers don't force queue infrastructure on generation-only users. |
| ADR-009 | `.ToCsv()` / `.ToExcel()` as sync config, `.GenerateAsync()` as terminal | **Accepted** | Avoids async chaining problems. Clean separation between "what to do" (configuration) and "do it" (execution). |
| ADR-010 | `ReportGen.Core` has zero third-party dependencies | **Accepted** | Core is the foundation package. Forcing any transitive dependency on it forces that dependency on every consumer and every custom exporter author. |
| ADR-011 | Domain events use `abstract record` base, not marker interfaces | **Accepted** | Records provide value equality and immutability. Abstract base type allows shared properties (`JobId`, `Timestamp`) without repetition. Pattern matching works naturally on the type hierarchy. |
| ADR-012 | Delivery accepts `Stream` not `byte[]` | **Accepted** | Supports large reports without full memory materialization. `Stream` is the natural interop type for SMTP, HTTP, and cloud storage APIs. |
