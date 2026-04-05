using ReportGen.Core;
using ReportGen.Exporters;

namespace BasicUsage;

/// <summary>
/// Demo 1 — Fluent builder: one-off reports with computed columns,
/// column ordering, and multiple exporters in a single chain.
/// </summary>
public static class Demo1_FluentBuilder
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n════════════════════════════════════════════");
        Console.WriteLine(" Demo 1 — Fluent Builder");
        Console.WriteLine("════════════════════════════════════════════");

        var employees = DataSeeder.Employees();

        // ----------------------------------------------------------
        // 1a. Basic employee roster — CSV + Excel in one chain
        // ----------------------------------------------------------
        await Report.Create("Employee Roster")
            .From(employees)
            .AddColumn("ID",          x => x.Id)
            .AddColumn("Name",        x => x.Name)
            .AddColumn("Department",  x => x.Department)
            .AddColumn("Email",       x => x.Email)
            .AddColumn("Active",      x => x.IsActive ? "Yes" : "No")
            .AddColumn("Joined",      x => x.JoinedOn.ToString("yyyy-MM-dd"))
            .ToCsv("./reports/demo1_roster.csv")
            .ToExcel("./reports/demo1_roster.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [1a] Employee roster    → demo1_roster.csv + .xlsx  (50 rows)");

        // ----------------------------------------------------------
        // 1b. Derived/computed columns — salary band + annual cost
        // ----------------------------------------------------------
        await Report.Create("Compensation Overview")
            .From(employees)
            .AddColumn("Name",         x => x.Name)
            .AddColumn("Department",   x => x.Department)
            .AddColumn("Salary",       x => x.Salary)
            .AddColumn("Band",         x => x.Salary switch
            {
                < 60_000m  => "Junior",
                < 100_000m => "Mid",
                < 130_000m => "Senior",
                _          => "Principal"
            })
            .AddColumn("Annual Cost",  x => x.Salary * 12)
            .AddColumn("Years Exp.",   x => x.YearsOfExperience)
            .ToCsv("./reports/demo1_compensation.csv")
            .ToExcel("./reports/demo1_compensation.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [1b] Compensation       → demo1_compensation.csv + .xlsx");

        // ----------------------------------------------------------
        // 1c. Filtered data (LINQ before .From()) — active only
        // ----------------------------------------------------------
        var activeOnly = employees.Where(e => e.IsActive).ToList();

        await Report.Create("Active Employees")
            .From(activeOnly)
            .AddColumn("Name",        x => x.Name)
            .AddColumn("Department",  x => x.Department)
            .AddColumn("Email",       x => x.Email)
            .AddColumn("Years",       x => x.YearsOfExperience)
            .ToCsv("./reports/demo1_active.csv")
            .GenerateAsync();

        Console.WriteLine($"  [1c] Active filter      → demo1_active.csv  ({activeOnly.Count} rows)");

        // ----------------------------------------------------------
        // 1d. Sorted + grouped label — sorted by salary DESC
        // ----------------------------------------------------------
        var sorted = employees.OrderByDescending(e => e.Salary).ToList();

        await Report.Create("Salary Leaderboard")
            .From(sorted)
            .AddColumn("Rank",       x => sorted.IndexOf(x) + 1)
            .AddColumn("Name",       x => x.Name)
            .AddColumn("Department", x => x.Department)
            .AddColumn("Salary",     x => x.Salary)
            .ToExcel("./reports/demo1_leaderboard.xlsx")
            .GenerateAsync();

        Console.WriteLine("  [1d] Salary leaderboard → demo1_leaderboard.xlsx  (sorted by salary)");
    }
}
