using System;
using MonstersMap;

Console.WriteLine("MonstersMap CLI — interactive search");
Console.WriteLine("Type a monster name (or :list, :quit):");

var candidates = MonsterSampleData.DefaultCandidates.ToList();

while (true)
{
    Console.Write("Name> ");
    var line = Console.ReadLine();
    if (line is null) break;
    line = line.Trim();
    if (line.Length == 0) continue;
    if (line.Equals(":quit", StringComparison.OrdinalIgnoreCase) || line.Equals(":")) break;
    if (line.Equals(":list", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Candidates:");
        foreach (var c in candidates) Console.WriteLine("- " + c.Name);
        continue;
    }

    var results = MonsterSearch.FindMatches(candidates, line);
    if (results.Count == 0)
    {
        Console.WriteLine($"No monsters found matching '{line}'.");
        continue;
    }

    Console.WriteLine($"Found {results.Count} monster(s) matching '{line}':");
    foreach (var r in results)
    {
        Console.WriteLine($"- {r.Name}");
        Console.WriteLine($"  Position: X={r.Position.X:0.00} Y={r.Position.Y:0.00} Z={r.Position.Z:0.00}");
    }
}

Console.WriteLine("Bye.");
