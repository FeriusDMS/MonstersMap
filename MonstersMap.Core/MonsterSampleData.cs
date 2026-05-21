using System.Collections.Generic;
using System.Numerics;

namespace MonstersMap;

public static class MonsterSampleData
{
    public static IReadOnlyList<MonsterSearchCandidate> DefaultCandidates { get; } = new[]
    {
        BuildCandidate("Muu Shuwuu", 0),
        BuildCandidate("Sanu Vali", 1),
        BuildCandidate("Baalzephon", 2),
        BuildCandidate("Stormslime", 3),
        BuildCandidate("Wild Hog", 4),
        BuildCandidate("Vautour", 5),
        BuildCandidate("imp", 6),
    };

    public static MonsterSearchCandidate BuildCandidate(string name, int index)
    {
        var offset = index + 1;
        return new MonsterSearchCandidate(name, new Vector3(offset, offset + 1, offset + 2));
    }

    public static IReadOnlyList<MonsterSearchCandidate> BuildCandidates(params string[] names)
    {
        var candidates = new List<MonsterSearchCandidate>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            candidates.Add(BuildCandidate(names[index], index));
        }

        return candidates;
    }
}
