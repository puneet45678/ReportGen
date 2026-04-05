namespace BasicUsage;

/// <summary>
/// Generates realistic-looking seed data for all demos.
/// All data is deterministic (fixed seed) so output files are reproducible.
/// </summary>
public static class DataSeeder
{
    private static readonly Random Rng = new(42);

    private static readonly string[] FirstNames =
    [
        "Ava", "Noah", "Mia", "Liam", "Sophia", "Ethan", "Isabella", "Mason",
        "Emma", "Lucas", "Olivia", "James", "Amelia", "Aiden", "Harper",
        "Logan", "Evelyn", "Elijah", "Abigail", "Oliver", "Emily", "Benjamin",
        "Charlotte", "Sebastian", "Sofia", "Jack", "Avery", "Owen", "Ella",
        "Samuel", "Scarlett", "Henry", "Grace", "Alexander", "Chloe", "Michael",
        "Zoey", "Daniel", "Riley", "Matthew", "Nora", "Joseph", "Lily",
        "David", "Eleanor", "Carter", "Hannah", "Wyatt", "Lillian", "John"
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller",
        "Davis", "Wilson", "Taylor", "Anderson", "Thomas", "Jackson", "White",
        "Harris", "Martin", "Thompson", "Young", "Robinson", "Clark", "Lewis",
        "Walker", "Hall", "Allen", "Wright", "King", "Scott", "Green", "Baker",
        "Adams", "Nelson", "Carter", "Mitchell", "Perez", "Roberts", "Turner",
        "Phillips", "Campbell", "Parker", "Evans", "Edwards", "Collins"
    ];

    private static readonly string[] Departments =
        ["Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Legal", "Product"];

    private static readonly string[] Regions =
        ["North", "South", "East", "West", "Central", "International"];

    private static readonly string[] Products =
    [
        "Laptop Pro 15", "Wireless Mouse", "Mechanical Keyboard", "USB-C Hub",
        "Monitor 27\"", "Standing Desk", "Webcam HD", "Noise-Cancelling Headset",
        "Ergonomic Chair", "Laptop Stand", "External SSD 1TB", "Smart Whiteboard"
    ];

    private static readonly string[] Categories =
        ["Electronics", "Furniture", "Accessories", "Peripherals", "Storage"];

    // -----------------------------------------------------------------
    // 50 employees across all departments
    // -----------------------------------------------------------------
    public static IReadOnlyList<Employee> Employees()
    {
        var list = new List<Employee>();
        for (var i = 1; i <= 50; i++)
        {
            var first = FirstNames[Rng.Next(FirstNames.Length)];
            var last = LastNames[Rng.Next(LastNames.Length)];
            var dept = Departments[Rng.Next(Departments.Length)];
            var years = Rng.Next(0, 16);
            var joinDate = DateTime.Today.AddYears(-years).AddDays(-Rng.Next(0, 365));
            var salary = Math.Round(40_000m + Rng.Next(0, 100_000), 2);

            list.Add(new Employee(
                Id: i,
                Name: $"{first} {last}",
                Department: dept,
                Email: $"{first.ToLower()}.{last.ToLower()}@company.com",
                Salary: salary,
                YearsOfExperience: years,
                JoinedOn: joinDate,
                IsActive: Rng.Next(0, 10) > 1));  // 80% active
        }
        return list;
    }

    // -----------------------------------------------------------------
    // 100 sales orders spread across regions and products
    // -----------------------------------------------------------------
    public static IReadOnlyList<SalesOrder> SalesOrders()
    {
        var list = new List<SalesOrder>();
        for (var i = 1; i <= 100; i++)
        {
            var customerFirst = FirstNames[Rng.Next(FirstNames.Length)];
            var customerLast = LastNames[Rng.Next(LastNames.Length)];
            var product = Products[Rng.Next(Products.Length)];
            var qty = Rng.Next(1, 20);
            var price = Math.Round(15m + Rng.Next(0, 2000), 2);
            var daysAgo = Rng.Next(0, 365);

            list.Add(new SalesOrder(
                OrderId: 1000 + i,
                CustomerName: $"{customerFirst} {customerLast}",
                Region: Regions[Rng.Next(Regions.Length)],
                Product: product,
                Quantity: qty,
                UnitPrice: price,
                OrderDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo)),
                IsShipped: Rng.Next(0, 10) > 2)); // 70% shipped
        }
        return list;
    }

    // -----------------------------------------------------------------
    // 30 product snapshots for the attribute-discovery demo
    // -----------------------------------------------------------------
    public static IReadOnlyList<ProductSnapshot> ProductSnapshots()
    {
        var list = new List<ProductSnapshot>();
        for (var i = 1; i <= 30; i++)
        {
            var product = Products[i % Products.Length];
            var category = Categories[Rng.Next(Categories.Length)];
            var daysAgo = Rng.Next(0, 180);

            list.Add(new ProductSnapshot
            {
                Id = 100 + i,
                Name = $"{product} (v{i % 3 + 1})",
                Category = category,
                UnitPrice = Math.Round(10m + Rng.Next(0, 3000), 2),
                StockLevel = Rng.Next(0, 500),
                LastRestocked = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo)),
                InternalCode = $"INT-{i:D4}"  // excluded from report — no attribute
            });
        }
        return list;
    }
}
