# ReportGen — Contract Design Guide

> A developer-level walkthrough of every contract in ReportGen.Core.
> Why each exists, what problem it solves, how they connect, and which
> software engineering patterns they keep alive.

---

## Table of Contents

1. [The Big Picture — What Are We Building?](#1-the-big-picture)
2. [The Core Problem — Why Contracts at All?](#2-the-core-problem)
3. [Contract #1: ColumnDefinition\<T\> — One Column's Shape](#3-columndefinition)
4. [Contract #2: ReportDefinition\<T\> — The Frozen Blueprint](#4-reportdefinition)
5. [Contract #3: IReportExporter — The Output Plug](#5-ireportexporter)
6. [Contract #4: IReportBuilderSource — The Starting Gate](#6-ireportbuildersource)
7. [Contract #5: IReportBuilder\<T\> — The Typed Workshop](#7-ireportbuilder)
8. [Contract #6: Report — The Front Door](#8-report)
9. [Contract #7: ReportTemplate\<T\> — The Reusable Form](#9-reporttemplate)
10. [Contract #8: IReportTemplateBuilder\<T\> — The Form Designer](#10-ireporttemplatebuilder)
11. [Contract #9: ReportColumnAttribute — The DTO Decorator](#11-reportcolumnattribute)
12. [Contract #10: ReportColumnExtensions — The Bridge](#12-reportcolumnextensions)
13. [How Everything Connects — The Full Flow](#13-full-flow)
14. [Pattern Reference — Why This Design Holds Up](#14-pattern-reference)

---

<a id="1-the-big-picture"></a>
## 1. The Big Picture — What Are We Building?

ReportGen turns data (a list of C# objects) into output files (CSV, Excel, etc.).

That sounds simple: loop through objects, write values. But if you hardcode that logic, you get:

- Column names buried inside CSV-writing code
- The same column list copy-pasted for every output format
- No way to test "did I define the right columns?" without actually writing a file
- Adding a new format (PDF) means touching existing code everywhere

**The goal of our contracts:** separate *what the report looks like* from *how it gets written*, so each piece can change independently.

---

<a id="2-the-core-problem"></a>
## 2. The Core Problem — Why Contracts at All?

### The naive approach

```csharp
// Everything mixed together
var lines = new List<string> { "Name,Email,Score" };     // column definition
foreach (var u in users)                                   // data access
    lines.Add($"{u.Name},{u.Email},{u.Score}");            // format logic (CSV)
File.WriteAllLines("report.csv", lines);                   // output destination
```

Four different concerns in 4 lines. Now your boss says "also give me an Excel version." You copy-paste the whole thing and rewrite it with ClosedXML. Same columns, same data, different format — but duplicated code.

Then a column changes. You update CSV. You forget Excel. Bug.

### What contracts give you

```
    "What columns?"     →   ColumnDefinition<T>
    "What data?"        →   ReportDefinition<T>
    "Write it where?"   →   IReportExporter
```

Each concern lives in its own type. The CSV exporter doesn't know about columns. The column definitions don't know about CSV. They communicate through a shared contract (`ReportDefinition<T>`), and that's it.

---

<a id="3-columndefinition"></a>
## 3. Contract #1: ColumnDefinition\<T\>

### The actual code

```csharp
public sealed record ColumnDefinition<T>(
    string Header,
    Func<T, object?> Accessor,
    int Order);
```

### What it represents

One column in a report. Just one. If your report has 5 columns, you have 5 `ColumnDefinition` objects.

### What each property does

| Property | Role | Example |
|---|---|---|
| `Header` | The text at the top of the column | `"Employee Name"` |
| `Accessor` | A function that pulls a value from one row | `x => x.Name` |
| `Order` | Position — 0 means first column, 1 means second, etc. | `0` |

### Why `Func<T, object?>` instead of a string property name?

**Option A — String-based (rejected):**
```csharp
new ColumnDefinition("Name", "Name")   // header, property name
// At runtime: uses reflection to read user.Name
// Problem: rename property → crash at runtime, not compile time
```

**Option B — Lambda-based (chosen):**
```csharp
new ColumnDefinition<User>("Name", x => x.Name, 0)
// At compile time: compiler verifies x.Name exists
// Bonus: supports computed values like x => $"{x.First} {x.Last}"
// Bonus: supports nesting like x => x.Address.City
```

The lambda approach gives you:
- **Compile-time safety** — rename a property, the compiler catches it
- **Computed columns** — any C# expression works: `x => x.Revenue / x.Units`
- **Nested access** — `x => x.Department.Manager.Email` just works
- **No reflection** — faster, no magic strings

### Why `sealed record`?

| Keyword | What it gives us |
|---|---|
| `record` | Value equality (two ColumnDefinitions with same Header/Accessor/Order are equal), built-in `ToString()`, immutability by default |
| `sealed` | Nobody can inherit and add mutable state — protects the immutability guarantee |

### What pattern does this implement?

**Value Object** (Domain-Driven Design). A `ColumnDefinition` has no identity — it's defined entirely by its values. Two columns with the same header, accessor, and order are interchangeable. That's a value object.

---

<a id="4-reportdefinition"></a>
## 4. Contract #2: ReportDefinition\<T\>

### The actual code

```csharp
public sealed record ReportDefinition<T>
{
    public required string Title { get; init; }
    public required IReadOnlyList<ColumnDefinition<T>> Columns { get; init; }
    public required IReadOnlyList<T> Data { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

### What it represents

A **complete, frozen snapshot** of a report: its title, its columns, and its data. Everything an exporter needs to produce output. Nothing more, nothing less.

### Why this type exists — the boundary object

This is the most important architectural role in the entire library. It's the **handoff point**:

```
Builder (constructs) ──► ReportDefinition<T> ◄── Exporter (consumes)
                              │
                    The ONLY type both sides share
```

Without this type, the builder would need to know about exporters ("give me your file path") and exporters would need to know about the builder ("what columns did you add?"). Everything would depend on everything.

With this type, neither side knows the other exists. The builder produces a `ReportDefinition`. The exporter consumes a `ReportDefinition`. They never talk directly. This is called **Dependency Inversion** — both depend on an abstraction, not on each other.

### Why `IReadOnlyList` and not `List`?

```csharp
// With List<T> — anyone can corrupt the data
report.Data.Add(fakeRow);           // ← exporter could inject data
report.Columns.RemoveAt(0);         // ← exporter could delete columns

// With IReadOnlyList<T> — compiler prevents it
report.Data.Add(fakeRow);           // ❌ compile error: no Add method
report.Columns.RemoveAt(0);         // ❌ compile error: no RemoveAt method
```

Once the report is built, the data and columns are **locked**. No exporter can accidentally (or maliciously) modify them. If you run 3 exporters sequentially, they all see the exact same data.

### Why `required`?

```csharp
// Without required — oops, forgot the title
var bad = new ReportDefinition<User> { Columns = cols, Data = data };
// Title is null — crashes later when exporter tries to use it

// With required — compiler catches it immediately
var bad = new ReportDefinition<User> { Columns = cols, Data = data };
// ❌ compile error: 'Title' is required
```

`required` means you cannot create a `ReportDefinition` without providing `Title`, `Columns`, and `Data`. No half-built objects floating around.

### Why `GeneratedAtUtc` has a default?

It's **metadata** — useful for audit trails ("this report was built at 2:30 PM"). It defaults to `DateTimeOffset.UtcNow` so consumers don't need to set it. But they *can* override it, which is critical for testing:

```csharp
// In a test — use fixed time so assertions aren't flaky
var def = new ReportDefinition<User> {
    Title = "Test",
    Columns = cols,
    Data = data,
    GeneratedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
};
```

### What patterns does this implement?

1. **Data Transfer Object (DTO)** — carries data across a boundary (builder → exporter) without behavior
2. **Immutable Object** — once created, cannot be changed (thread-safe, predictable)
3. **Boundary Object** — the single shared contract between two systems that don't know about each other

---

<a id="5-ireportexporter"></a>
## 5. Contract #3: IReportExporter

### The actual code

```csharp
public interface IReportExporter
{
    Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken cancellationToken = default);
}
```

### What it represents

The "plug" that any output format implements. CSV, Excel, PDF, email sender, S3 uploader — they all implement this one interface.

### Why it's an interface and not a base class

```csharp
// Base class approach (rejected):
public abstract class ReportExporterBase
{
    public abstract Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken ct);
    // If we add helper methods here, ALL exporters inherit them
    // Even if they don't need them
    // And in C#, a class can only inherit ONE base class
    // So your exporter can't also inherit from some other class
}

// Interface approach (chosen):
public interface IReportExporter
{
    Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken ct);
}
// An exporter can implement IReportExporter AND other interfaces
// No forced inheritance chain
// Maximum flexibility
```

### Why `<T>` is on the method, not the interface

This is a deliberate design choice. Compare:

```csharp
// Option A: T on interface
public interface IReportExporter<T>
{
    Task ExportAsync(ReportDefinition<T> report, CancellationToken ct);
}
// Usage: CsvExporter : IReportExporter<Employee>
// Problem: one CsvExporter instance works for Employee only
//          need another instance for SalesRecord
//          exporter tied to data type — makes no sense
//          a CSV writer doesn't care what the rows are

// Option B: T on method (our choice)
public interface IReportExporter
{
    Task ExportAsync<T>(ReportDefinition<T> report, CancellationToken ct);
}
// Usage: CsvExporter : IReportExporter
// One instance handles ANY T — Employee, SalesRecord, anything
// Because CSV writing is the same regardless of row type:
//   read header → write, read accessor(row) → write
```

A CSV exporter doesn't care if you're exporting employees or products. It reads column headers, calls accessors on rows, and writes text. The data type is irrelevant to the format. So `T` belongs on the method (per-call), not the interface (per-instance).

### Why `Task` (async)?

Writing files = I/O. Uploading to S3 = network call. Sending email = network call. These operations are inherently asynchronous. Making the contract async from the start means:

- No exporter ever needs to block threads
- `CancellationToken` flows naturally (more on that below)
- Async code calling sync code is easy; sync code calling async code causes deadlocks

### Why `CancellationToken`?

Reports can be large. The user might cancel. The HTTP request might time out. The server might be shutting down. Without a cancellation token:

```csharp
// No cancellation — you're stuck waiting for a 50MB Excel file
await exporter.ExportAsync(hugeReport);  // blocks until done, no way to stop
```

With a cancellation token:

```csharp
// Cancellable — stops as soon as possible
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await exporter.ExportAsync(report, cts.Token);
// If it takes too long, the token cancels and the exporter stops
```

This is the standard .NET cooperative cancellation pattern. By putting it on the interface, every exporter is forced to accept tokens — even if they don't use them today.

### What patterns does this implement?

1. **Strategy Pattern** — different algorithms (CSV, Excel, PDF) behind one interface, swappable at runtime
2. **Open-Closed Principle (SOLID)** — the system is open for new formats (implement the interface) but closed for modification (existing code doesn't change)
3. **Dependency Inversion (SOLID)** — the builder depends on `IReportExporter` (abstraction), not on `CsvExporter` (concrete class)

---

<a id="6-ireportbuildersource"></a>
## 6. Contract #4: IReportBuilderSource

### The actual code

```csharp
public interface IReportBuilderSource
{
    IReportBuilder<T> From<T>(IEnumerable<T> data);
}
```

### What it represents

The **first stage** of the builder — before you've told it what data you have. At this point, the library knows the report title but nothing else.

### Why this exists as a separate interface

This is about **enforcing the correct calling order at compile time**.

Imagine we had one single interface:

```csharp
// Hypothetical single interface (bad)
public interface IReportBuilder
{
    IReportBuilder From<T>(IEnumerable<T> data);
    IReportBuilder AddColumn<T>(string header, Func<T, object?> accessor);
    // ❌ What is T in AddColumn? No connection to From's T.
}
```

The consumer would have to manually specify `T` on every call:

```csharp
builder.From(users).AddColumn<User>("Name", x => x.Name);
//                            ^^^^^^ required — compiler can't infer
```

With two interfaces:

```csharp
IReportBuilderSource  (has: From<T>)
    │
    │  .From(users)  ← C# infers T = User
    ▼
IReportBuilder<User>  (has: AddColumn, AddExporter, Build, GenerateAsync)
    │
    │  .AddColumn("Name", x => x.Name)  ← T is known, x.Name just works
    ▼
```

After `.From(users)`, the returned type is `IReportBuilder<User>`. The `T` is baked into the interface. Every subsequent call knows `T = User` without you saying it.

And crucially — you **cannot** call `.AddColumn()` before `.From()`:

```csharp
Report.Create("Users").AddColumn("Name", x => x.Name);
// ❌ compile error: IReportBuilderSource has no AddColumn method
```

The compiler prevents wrong call order. Not a runtime error — a red squiggle in your editor before you even run the code.

### What pattern does this implement?

**Type-State Pattern** — the type of the object changes as you transition through stages. Each stage exposes only the methods that make sense at that point. The compiler enforces valid transitions.

### The internal implementation

```csharp
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
```

It holds the title, waits for `.From()`, validates the data isn't null, creates the Phase 2 builder, and passes the title along. A stepping stone — simple and intentional.

`internal sealed` means consumers never see this class. They interact through `IReportBuilderSource` only.

---

<a id="7-ireportbuilder"></a>
## 7. Contract #5: IReportBuilder\<T\>

### The actual code

```csharp
public interface IReportBuilder<T>
{
    IReportBuilder<T> AddColumn(string header, Func<T, object?> accessor);
    IReportBuilder<T> AddExporter(IReportExporter exporter);
    ReportDefinition<T> Build();
    Task GenerateAsync(CancellationToken cancellationToken = default);
}
```

### What it represents

The **second stage** of the builder — after data is bound. `T` is known. You can now add columns, add exporters, build the definition, or generate the report.

### Why each method returns `IReportBuilder<T>`

This enables **method chaining** (the "fluent" API):

```csharp
Report.Create("Users")
    .From(users)               // returns IReportBuilder<User>
    .AddColumn("Name", ...)    // returns IReportBuilder<User>
    .AddColumn("Email", ...)   // returns IReportBuilder<User>
    .ToCsv("out.csv")          // returns IReportBuilder<User>  (extension method)
    .GenerateAsync();           // terminal — returns Task
```

Each call returns the same builder, allowing the next call to chain. Reads like a sentence. Without this, you'd write:

```csharp
var builder = Report.Create("Users").From(users);
builder.AddColumn("Name", ...);
builder.AddColumn("Email", ...);
builder.ToCsv("out.csv");
await builder.GenerateAsync();
```

Same result, but 5 statements instead of a single expression. The fluent version is more compact and harder to mess up (you can't forget a step in the middle).

### Why `Build()` and `GenerateAsync()` are separate

`Build()` creates the `ReportDefinition<T>` without running any exporters. Why useful?

1. **Testing** — you want to verify "did I configure the right columns?" without actually writing files:
   ```csharp
   var def = builder.Build();
   Assert.Equal(3, def.Columns.Count);      // test column count
   Assert.Equal("Name", def.Columns[0].Header);  // test headers
   ```

2. **Inspection** — middleware or logging that needs to examine the report before export.

3. **Reuse** — pass the same definition to multiple exporters manually.

`GenerateAsync()` calls `Build()` internally and then runs all exporters. It's the "do everything" shortcut.

### The internal implementation — key details

**Data materialization happens in `Build()`:**

```csharp
Data = _data.ToList().AsReadOnly()
```

`_data` is stored as `IEnumerable<T>` — it could be a LINQ query, a database cursor, etc. It hasn't run yet. `.ToList()` forces execution: "go get the actual data now." `.AsReadOnly()` wraps it so nobody can modify it.

Why delay until `Build()`? Because the consumer might still be building a query:
```csharp
var query = db.Users.Where(u => u.Active);   // not executed yet
var builder = Report.Create("Users").From(query);
// ... more query building could happen ...
builder.Build();  // NOW the query executes
```

**Exporters run sequentially in `GenerateAsync()`:**

```csharp
foreach (var exporter in _exporters)
{
    cancellationToken.ThrowIfCancellationRequested();
    await exporter.ExportAsync(definition, cancellationToken).ConfigureAwait(false);
}
```

Sequential, not parallel. Why? Simpler to debug, predictable order, and if one fails you know which one. Parallel execution would be a premature optimization.

### What patterns does this implement?

1. **Builder Pattern** — collects configuration piece by piece, then produces an immutable result. Mutable during construction, immutable after `Build()`.
2. **Fluent Interface** — method chaining for readability.
3. **Type-State Pattern** (continued) — `IReportBuilder<T>` is only reachable after `IReportBuilderSource.From()`.

---

<a id="8-report"></a>
## 8. Contract #6: Report

### The actual code

```csharp
public static class Report
{
    public static IReportBuilderSource Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Internal.ReportBuilderSource(title);
    }
}
```

### What it represents

The **front door**. The one and only entry point. Every report starts here.

### Why a static class with a static method

**Discoverability.** A consumer opens their IDE, types `Report.`, and sees `Create`. One entry point, impossible to miss. Compare:

```csharp
// Our approach — obvious start
Report.Create("Users")

// Alternative — what class do I use?
new ReportBuilder("Users")       // or is it ReportBuilderFactory?
new ReportConfiguration("Users") // or ReportManager?
ReportFactory.New("Users")       // too many choices
```

**Hides internals.** The return type is `IReportBuilderSource` — an interface. The consumer never sees `ReportBuilderSource` (the concrete class). We can completely rewrite the implementation without breaking any consumer code.

**Validates at the boundary.** An empty title makes no sense. Catching it here — at the very first call — gives a clear error:

```csharp
Report.Create("");
// → ArgumentException: "title cannot be null or whitespace"
// Clear. Immediate. Instead of crashing deep inside an exporter.
```

### What pattern does this implement?

**Static Factory Method** — an alternative to constructors. Advantages over `new`:
- Can return an interface type (constructors always return the concrete type)
- Can validate and fail before creating anything
- Name is descriptive (`Create` vs `new ReportBuilderSource`)

---

<a id="9-reporttemplate"></a>
## 9. Contract #7: ReportTemplate\<T\>

### The actual code

```csharp
public sealed class ReportTemplate<T>
{
    public string Title { get; }
    public IReadOnlyList<ColumnDefinition<T>> Columns { get; }

    internal ReportTemplate(string title, IReadOnlyList<ColumnDefinition<T>> columns) { ... }

    public IReportBuilder<T> From(IEnumerable<T> data) => From(data, title: null);

    public IReportBuilder<T> From(IEnumerable<T> data, string? title)
    {
        ArgumentNullException.ThrowIfNull(data);
        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? Title : title;
        var builder = new Internal.ReportBuilder<T>(effectiveTitle, data);
        foreach (var column in Columns)
            builder.AddColumn(column.Header, column.Accessor);
        return builder;
    }

    public static IReportTemplateBuilder<T> Define(string title) { ... }
}
```

### The real-world analogy

A **printed expense form** at your office. The form has a title ("Monthly Expenses") and column headers (Date, Amount, Description). Every month you grab a blank copy, fill in different expenses, and submit. The form itself never changes.

### The problem it solves

Without templates, you repeat the same columns every time:

```csharp
// January — define 3 columns
await Report.Create("Expenses").From(janData)
    .AddColumn("Date", x => x.Date)
    .AddColumn("Amount", x => x.Amount)
    .AddColumn("Description", x => x.Description)
    .ToCsv("jan.csv").GenerateAsync();

// February — same 3 columns, copy-pasted
await Report.Create("Expenses").From(febData)
    .AddColumn("Date", x => x.Date)           // duplicated
    .AddColumn("Amount", x => x.Amount)        // duplicated
    .AddColumn("Description", x => x.Description)  // duplicated
    .ToCsv("feb.csv").GenerateAsync();
```

With a template:

```csharp
// Define once
var expenseForm = ReportTemplate<Expense>.Define("Monthly Expenses")
    .AddColumn("Date", x => x.Date)
    .AddColumn("Amount", x => x.Amount)
    .AddColumn("Description", x => x.Description)
    .Build();

// Use many times — just provide data
await expenseForm.From(janData).ToCsv("jan.csv").GenerateAsync();
await expenseForm.From(febData).ToCsv("feb.csv").GenerateAsync();
```

Change a column? One place. Works for all months.

### How `From()` keeps the template safe

This is the critical mechanism. Here's what happens inside `From()`:

```csharp
public IReportBuilder<T> From(IEnumerable<T> data, string? title)
{
    // 1. Create a BRAND NEW builder
    var builder = new Internal.ReportBuilder<T>(effectiveTitle, data);

    // 2. COPY columns from template into the new builder
    foreach (var column in Columns)
        builder.AddColumn(column.Header, column.Accessor);

    // 3. Return the independent builder
    return builder;
}
```

The word **COPY** is key. The new builder gets its own separate list of columns. The template's `Columns` list is `IReadOnlyList` — it cannot be modified. So:

```csharp
var builder1 = template.From(janData);
builder1.AddColumn("Extra", x => x.Notes);  // adds to builder1's list ONLY

var builder2 = template.From(febData);
// builder2 has only the template columns — "Extra" doesn't leak across
```

Each `.From()` = a fresh photocopy. Write on one, the original stays clean.

### Why `sealed class` and not `sealed record`?

- `record` is for data carriers you might compare by value. You don't compare templates — you create one and reuse it.
- `class` is for long-lived objects with identity. A template is a "thing" in your app, often registered in dependency injection as a singleton.

### Why `internal` constructor?

```csharp
new ReportTemplate<User>("Users", columns);
// ❌ Can't do this from outside the library

ReportTemplate<User>.Define("Users").AddColumn(...).Build();
// ✅ The only way — forces you through validation
```

No way to accidentally create a template with zero columns or an empty title.

### What patterns does this implement?

1. **Template Method** (conceptual, not the GoF pattern) — a fixed structure (columns) with variable data
2. **Prototype Pattern** — `From()` creates independent copies pre-configured from the template
3. **Immutable Object** — thread-safe, DI-singleton-safe, no locks needed

---

<a id="10-ireporttemplatebuilder"></a>
## 10. Contract #8: IReportTemplateBuilder\<T\>

### The actual code

```csharp
public interface IReportTemplateBuilder<T>
{
    IReportTemplateBuilder<T> AddColumn(string header, Func<T, object?> accessor);
    ReportTemplate<T> Build();
}
```

### Why it's separate from `ReportTemplate<T>`

If one class did both building and using:

```csharp
// Hypothetical combined class
template.AddColumn("Date", x => x.Date);  // building
template.From(janData);                     // using
template.AddColumn("Oops", x => x.Notes);  // building AFTER using — bug!
```

You could modify the template after binding data. Reports generated before and after the change silently differ. A bug that's nearly impossible to catch.

Two separate types enforce two clear states:

| Type | Can add columns? | Can bind data? |
|---|---|---|
| `IReportTemplateBuilder<T>` | Yes | No — method doesn't exist |
| `ReportTemplate<T>` | No — method doesn't exist | Yes |

`.Build()` is the one-way door between them:

```csharp
// Designing (builder)
builder.AddColumn("Date", x => x.Date);    // ✅
builder.From(data);                          // ❌ compile error

// Using (template)
template.From(data);                         // ✅
template.AddColumn("Date", x => x.Date);    // ❌ compile error
```

The compiler enforces the lifecycle. You physically cannot use the wrong method at the wrong time.

### What pattern does this implement?

**Builder Pattern** — same as `IReportBuilder<T>`. Collects configuration, then produces an immutable result. The built result (`ReportTemplate<T>`) is deliberately a different type with different capabilities.

---

<a id="11-reportcolumnattribute"></a>
## 11. Contract #9: ReportColumnAttribute

### The actual code

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ReportColumnAttribute : Attribute
{
    public string? Header { get; }
    public int Order { get; set; } = int.MaxValue;

    public ReportColumnAttribute() { }
    public ReportColumnAttribute(string header) => Header = header;
}
```

### The problem it solves

With the lambda approach, you define columns separately from your data class:

```csharp
// The class
public class Employee { public string Name { get; set; } ... }

// The report — columns defined elsewhere
.AddColumn("Employee Name", x => x.Name)
.AddColumn("Email", x => x.Email)
```

If your class has 15 properties and you always want the same 15 columns, that's 15 `.AddColumn()` calls. The attribute approach lets you declare columns directly on the class:

```csharp
public class Employee
{
    [ReportColumn("Employee Name", Order = 0)]
    public string Name { get; set; }

    [ReportColumn("Email", Order = 1)]
    public string Email { get; set; }

    public string InternalSecret { get; set; }  // no attribute = excluded
}
```

Then usage is one line: `.AddColumnsFromAttributes()`.

### Design decisions

**`AllowMultiple = false`** — a property can't be two different columns. One attribute per property.

**`Header` is optional** — if you write `[ReportColumn]` without a header, the property name is used. Less typing for simple cases where the property name is already good enough.

**`Order` defaults to `int.MaxValue`** — unordered columns go to the end. Only set `Order` if you care about specific positioning.

### When to use attributes vs lambdas

| Situation | Use |
|---|---|
| Simple DTO where all columns match properties | Attributes — less code |
| Same entity, multiple different reports | Lambdas — full control per report |
| Computed columns (`FirstName + LastName`) | Lambdas — can't compute in an attribute |
| Third-party class you can't modify | Lambdas — can't add attributes to someone else's code |
| Mix of both | Both — `.AddColumnsFromAttributes()` then `.AddColumn(...)` |

### What pattern does this implement?

**Declarative Programming / Metadata-driven Design** — the same pattern Entity Framework uses with `[Key]`, `[Required]`, etc. You annotate your model with intent, and the framework acts on it.

---

<a id="12-reportcolumnextensions"></a>
## 12. Contract #10: ReportColumnExtensions

### The actual code (simplified)

```csharp
public static class ReportColumnExtensions
{
    public static IReportBuilder<T> AddColumnsFromAttributes<T>(this IReportBuilder<T> builder) { ... }
    public static IReportTemplateBuilder<T> AddColumnsFromAttributes<T>(this IReportTemplateBuilder<T> builder) { ... }

    private static IEnumerable<(string Header, Func<T, object?> Accessor)> DiscoverColumns<T>() { ... }
}
```

### What it does

Reads `[ReportColumn]` attributes from the properties of `T` using **reflection**, then calls `.AddColumn()` for each one — converting the attribute metadata into the same `ColumnDefinition<T>` objects that manual `.AddColumn()` creates.

### Why it's an extension method class, not built into the builder

1. **Separation of concerns.** The builder shouldn't know about reflection or attributes. It knows about columns. The extension bridges the gap.

2. **Optional dependency.** If a consumer never uses attributes, the reflection code is never loaded. No cost for what you don't use.

3. **Works on both builders.** One overload for `IReportBuilder<T>`, another for `IReportTemplateBuilder<T>`. Same logic, different targets.

### The reflection → lambda bridge

```csharp
var prop = property;
Func<T, object?> accessor = row => prop.GetValue(row);
```

This converts a `PropertyInfo` (reflection metadata) into a `Func<T, object?>` (lambda). The rest of the system — `ColumnDefinition`, `ReportBuilder`, exporters — never knows the column came from an attribute. They see the same `Func<T, object?>` accessor as a manually written `x => x.Name`.

This is the power of the contract-based design: **the attribute layer plugs into the existing system without changing a single line of existing code.**

### What pattern does this implement?

**Adapter Pattern** — converts one interface (reflection metadata) into another (lambda accessors) so existing code can work with it.

---

<a id="13-full-flow"></a>
## 13. How Everything Connects — The Full Flow

### Flow 1: Manual columns (one-off report)

```
Report.Create("Users")                   → IReportBuilderSource
    .From(users)                         → IReportBuilder<User>
    .AddColumn("Name", x => x.Name)      → IReportBuilder<User>
    .AddColumn("Email", x => x.Email)     → IReportBuilder<User>
    .AddExporter(new CsvExporter("f.csv"))→ IReportBuilder<User>
    .GenerateAsync()                      → Task
        ├── Build()
        │   └── ReportDefinition<User> created (columns + data frozen)
        └── foreach exporter
            └── exporter.ExportAsync(definition)
```

### Flow 2: Attribute-based columns

```
Report.Create("Users")                   → IReportBuilderSource
    .From(users)                         → IReportBuilder<User>
    .AddColumnsFromAttributes()          → IReportBuilder<User>
    │   └── reflection reads [ReportColumn] from User properties
    │   └── calls .AddColumn() for each — same path as Flow 1
    .ToCsv("f.csv")                      → IReportBuilder<User>
    .GenerateAsync()                      → Task
```

### Flow 3: Template with data binding

```
ReportTemplate<Sale>.Define("Sales")     → IReportTemplateBuilder<Sale>
    .AddColumn("Product", x => x.Product) → IReportTemplateBuilder<Sale>
    .Build()                              → ReportTemplate<Sale> (frozen)

template.From(marchData)                 → IReportBuilder<Sale> (fresh copy)
    .ToCsv("march.csv")                  → IReportBuilder<Sale>
    .GenerateAsync()                      → Task

template.From(aprilData, "April Sales")  → IReportBuilder<Sale> (another fresh copy)
    .ToExcel("april.xlsx")               → IReportBuilder<Sale>
    .GenerateAsync()                      → Task
```

### Flow 4: Mixed — attributes + manual + template

```
ReportTemplate<Employee>.Define("Team")  → IReportTemplateBuilder<Employee>
    .AddColumnsFromAttributes()          → IReportTemplateBuilder<Employee>
    .AddColumn("Full Name",              → IReportTemplateBuilder<Employee>
        x => $"{x.First} {x.Last}")
    .Build()                              → ReportTemplate<Employee>

template.From(teamData)                  → IReportBuilder<Employee>
    .AddColumn("Score Grade",            → IReportBuilder<Employee>
        x => x.Score > 90 ? "A" : "B")
    .ToCsv("team.csv")                   → IReportBuilder<Employee>
    .GenerateAsync()                      → Task
```

### The complete type map

```
                         ┌──────────────────────────┐
                         │   ReportColumnAttribute   │
                         │   (DTO decoration)        │
                         └────────────┬─────────────┘
                                      │ reflection
                         ┌────────────▼─────────────┐
                         │  ReportColumnExtensions   │
                         │  (attribute → lambda)     │
                         └────────────┬─────────────┘
                                      │ .AddColumn()
┌───────────┐           ┌─────────────▼────────────┐
│   Report   │──Create──►│  IReportBuilderSource    │
│  (entry)   │           │  (Phase 1: untyped)      │
└───────────┘           └─────────────┬────────────┘
                                      │ .From(data)
                         ┌────────────▼────────────┐
                         │   IReportBuilder<T>      │◄──── ReportTemplate<T>.From()
                         │   (Phase 2: typed)       │
                         └─────┬──────────────┬────┘
                               │ .Build()     │ .GenerateAsync()
                    ┌──────────▼──────┐       │
                    │ ReportDefinition │       │
                    │    <T>          │◄──────┘
                    └────────┬────────┘
                             │ consumed by
                    ┌────────▼────────┐
                    │  IReportExporter │
                    │  (CSV/Excel/...) │
                    └─────────────────┘

┌──────────────────────────────────────────────────┐
│  TEMPLATE PATH (parallel entry)                   │
│                                                    │
│  ReportTemplate<T>.Define()                        │
│      → IReportTemplateBuilder<T>                   │
│          .AddColumn() / .AddColumnsFromAttributes()│
│          .Build()                                  │
│      → ReportTemplate<T>  (frozen form)            │
│          .From(data)                               │
│      → IReportBuilder<T>  (joins main flow above)  │
└──────────────────────────────────────────────────┘
```

---

<a id="14-pattern-reference"></a>
## 14. Pattern Reference — Why This Design Holds Up

### SOLID Principles

| Principle | Where it shows up |
|---|---|
| **S — Single Responsibility** | Each type does one thing: `ColumnDefinition` = one column, `ReportDefinition` = frozen snapshot, `IReportExporter` = one output format, builder = assembles, template = reusable shape |
| **O — Open/Closed** | Adding a new exporter (PDF) = implement `IReportExporter`. No existing code changes. Adding attribute support = extension methods. No existing code changes. |
| **L — Liskov Substitution** | Any `IReportExporter` (CSV, Excel, PDF) can be used wherever an exporter is expected. The builder treats them identically. |
| **I — Interface Segregation** | `IReportBuilderSource` has 1 method. `IReportTemplateBuilder<T>` has 2. `IReportExporter` has 1. No "fat interfaces" forcing implementors to write methods they don't need. |
| **D — Dependency Inversion** | Builder depends on `IReportExporter` (abstraction), not `CsvExporter` (concrete). You can create an exporter the library has never seen, and it works. |

### Design Patterns

| Pattern | Where | Why |
|---|---|---|
| **Builder** | `ReportBuilder<T>`, `ReportTemplateBuilder<T>` | Collects configuration incrementally, produces immutable result |
| **Fluent Interface** | All builders return `this` | Method chaining for readable API |
| **Type-State** | `IReportBuilderSource` → `IReportBuilder<T>` | Compiler enforces valid call sequences |
| **Strategy** | `IReportExporter` implementations | Swap output format at runtime without changing builder logic |
| **Static Factory** | `Report.Create()`, `ReportTemplate<T>.Define()` | Clean entry points, hide implementation details |
| **Value Object** | `ColumnDefinition<T>` | Immutable, identity-free, equality by value |
| **Adapter** | `ReportColumnExtensions` | Bridges reflection world (attributes) to lambda world (accessors) |
| **Prototype** | `ReportTemplate<T>.From()` | Creates independent copies pre-loaded from a template |

### Immutability Boundaries

```
MUTABLE (during construction)          IMMUTABLE (after build)
─────────────────────────────          ──────────────────────
ReportBuilder<T>                  →    ReportDefinition<T>
  ._columns (List)                       .Columns (IReadOnlyList)
  ._exporters (List)                     .Data (IReadOnlyList)
  ._data (IEnumerable — lazy)           (.Data materialized via .ToList())

ReportTemplateBuilder<T>          →    ReportTemplate<T>
  ._columns (List)                       .Columns (IReadOnlyList)
                                         .Title (getter-only)
```

The `Build()` method is the **freeze point** in both cases. Before it: add, change, configure freely. After it: locked, safe, shareable.

### Validation Boundaries

```
ENTRY POINT                    WHAT'S VALIDATED
────────────                   ─────────────────
Report.Create(title)           title not null/whitespace
.From(data)                    data not null
.AddColumn(header, accessor)   header not null/whitespace, accessor not null
.AddExporter(exporter)         exporter not null
.Build()                       at least 1 column
.GenerateAsync()               at least 1 exporter

ReportTemplate<T>.Define(t)    title not null/whitespace
.AddColumn(header, accessor)   header not null/whitespace, accessor not null
.Build()                       at least 1 column

.AddColumnsFromAttributes()    at least 1 [ReportColumn] property on T
```

Every public method validates its inputs immediately. Invalid data never travels deeper into the system. Errors are caught where they were caused — not three method calls later in an exporter.

---

## Summary

Ten types. Each with a single clear job:

| # | Type | One-line job |
|---|---|---|
| 1 | `ColumnDefinition<T>` | Describes one column (header + how to get the value) |
| 2 | `ReportDefinition<T>` | Frozen snapshot — the handoff between builder and exporter |
| 3 | `IReportExporter` | "I can write a report to some format" |
| 4 | `IReportBuilderSource` | "Give me data so I can start building" |
| 5 | `IReportBuilder<T>` | "I'm collecting columns and exporters, ready to build" |
| 6 | `Report` | Front door — clean entry point |
| 7 | `ReportTemplate<T>` | Reusable frozen form — bind different data each time |
| 8 | `IReportTemplateBuilder<T>` | Designing the form before freezing it |
| 9 | `ReportColumnAttribute` | "This property is a report column" |
| 10 | `ReportColumnExtensions` | Converts attributes into the same column objects the builder uses |

None of these types know about the others more than necessary. Each can evolve, be tested, and be replaced independently. That's the whole point of contracts.
