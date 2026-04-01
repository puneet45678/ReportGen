using ReportGen.Core;
using ReportGen.Exporters;

// --- Sample data ---
var users = new[]
{
    new { Name = "Ava", Email = "ava@company.com", Score = 92 },
    new { Name = "Noah", Email = "noah@company.com", Score = 88 },
    new { Name = "Mia", Email = "mia@company.com", Score = 95 }
};

// =================================================
// Example 1: One-off report via fluent builder
// =================================================
Console.WriteLine("Example 1: One-off builder");

await Report.Create("User Performance")
    .From(users)
    .AddColumn("Name", x => x.Name)
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .ToCsv("./reports/users.csv")
    .ToExcel("./reports/users.xlsx")
    .GenerateAsync();

Console.WriteLine("  → reports/users.csv written");
Console.WriteLine("  → reports/users.xlsx written");

// =================================================
// Example 2: Reusable template
// =================================================
Console.WriteLine("\nExample 2: Reusable template");

var template = ReportTemplate<(string Name, string Email, int Score)>
    .Define("User Scores")
    .AddColumn("Name", x => x.Name)
    .AddColumn("Email", x => x.Email)
    .AddColumn("Score", x => x.Score)
    .Build();

var batch1 = new[] { (Name: "Ava", Email: "ava@co.com", Score: 92) };
var batch2 = new[] { (Name: "Noah", Email: "noah@co.com", Score: 88) };

await template.From(batch1, "Scores — Batch 1")
    .ToCsv("./reports/batch1.csv")
    .GenerateAsync();

await template.From(batch2, "Scores — Batch 2")
    .ToCsv("./reports/batch2.csv")
    .GenerateAsync();

Console.WriteLine("  → reports/batch1.csv written");
Console.WriteLine("  → reports/batch2.csv written");

Console.WriteLine("\nDone.");
