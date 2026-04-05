using BasicUsage;

Console.WriteLine("ReportGen — Sample Runner");
Console.WriteLine("Output files are written to ./reports/");
Directory.CreateDirectory("./reports");

var sw = System.Diagnostics.Stopwatch.StartNew();

await Demo1_FluentBuilder.RunAsync();
await Demo2_Template.RunAsync();
await Demo3_AttributeDiscovery.RunAsync();
await Demo4_StreamExport.RunAsync();
await Demo5_EdgeCases.RunAsync();

sw.Stop();
Console.WriteLine($"\n════════════════════════════════════════════");
Console.WriteLine($" All demos complete in {sw.ElapsedMilliseconds} ms");
Console.WriteLine($" Reports written to: {Path.GetFullPath("./reports")}");
Console.WriteLine($"════════════════════════════════════════════");
