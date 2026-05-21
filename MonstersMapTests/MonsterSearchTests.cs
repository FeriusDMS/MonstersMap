using System.Collections.Generic;
using System.Numerics;

using MonstersMap;

using Xunit;

namespace MonstersMapTests;

public class MonsterSearchTests
{
    [Fact]
    public void Finds_MultiWord_Names()
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Muu Shuwuu", new Vector3(12.4f, 18.7f, 3.1f)),
            new("Stormslime", new Vector3(5.5f, 27.9f, 2.2f)),
        };

        var results = MonsterSearch.FindMatches(candidates, "Muu Shuwuu");

        Assert.Single(results);
        Assert.Equal("Muu Shuwuu", results[0].Name);
        Assert.Equal(new Vector3(12.4f, 18.7f, 3.1f), results[0].Position);
    }

    [Fact]
    public void Finds_Partial_Names()
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Muu Shuwuu", new Vector3(12.4f, 18.7f, 3.1f)),
            new("Baalzephon", new Vector3(33.2f, 14.8f, 0.0f)),
        };

        var results = MonsterSearch.FindMatches(candidates, "muu");

        Assert.Single(results);
        Assert.Equal("Muu Shuwuu", results[0].Name);
        Assert.Equal(new Vector3(12.4f, 18.7f, 3.1f), results[0].Position);
    }

    [Theory]
    [MemberData(nameof(MultiMatchCases))]
    public void Supports_Varied_Search_Scenarios(
        string query,
        string[] expectedNames,
        Vector3[] expectedPositions)
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Muu Shuwuu", new Vector3(12.4f, 18.7f, 3.1f)),
            new("Sanu Vali", new Vector3(21.0f, 9.6f, -1.4f)),
            new("Baalzephon", new Vector3(33.2f, 14.8f, 0.0f)),
            new("Stormslime", new Vector3(5.5f, 27.9f, 2.2f)),
            new("Wild Hog", new Vector3(41.3f, 7.4f, -0.2f)),
        };

        var results = MonsterSearch.FindMatches(candidates, query);

        Assert.Equal(expectedNames.Length, results.Count);

        for (var index = 0; index < expectedNames.Length; index++)
        {
            Assert.Equal(expectedNames[index], results[index].Name);
            Assert.Equal(expectedPositions[index], results[index].Position);
        }
    }

    [Fact]
    public void Ignores_Accent_And_Whitespace_Noise()
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Vautour", new Vector3(16.8f, 22.1f, 1.0f)),
        };

        var results = MonsterSearch.FindMatches(candidates, "  Vautoúr  ");

        Assert.Single(results);
        Assert.Equal("Vautour", results[0].Name);
        Assert.Equal(new Vector3(16.8f, 22.1f, 1.0f), results[0].Position);
    }

    [Fact]
    public void Returns_Multiple_Matches_When_Query_Is_Generic()
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Muu Shuwuu", new Vector3(12.4f, 18.7f, 3.1f)),
            new("Wild Hog", new Vector3(41.3f, 7.4f, -0.2f)),
            new("Stormslime", new Vector3(5.5f, 27.9f, 2.2f)),
        };

        var results = MonsterSearch.FindMatches(candidates, "w");

        Assert.Equal(2, results.Count);
        Assert.Equal("Wild Hog", results[0].Name);
        Assert.Equal("Muu Shuwuu", results[1].Name);
    }

    [Fact]
    public void Returns_Empty_For_No_Match()
    {
        var candidates = new List<MonsterSearchCandidate>
        {
            new("Muu Shuwuu", new Vector3(12.4f, 18.7f, 3.1f)),
        };

        var results = MonsterSearch.FindMatches(candidates, "not-a-real-monster");

        Assert.Empty(results);
    }

    public static IEnumerable<object[]> MultiMatchCases()
    {
        yield return new object[]
        {
            "Muu Shuwuu",
            new[] { "Muu Shuwuu" },
            new[] { new Vector3(12.4f, 18.7f, 3.1f) },
        };

        yield return new object[]
        {
            "muu",
            new[] { "Muu Shuwuu" },
            new[] { new Vector3(12.4f, 18.7f, 3.1f) },
        };

        yield return new object[]
        {
            "sanu vali",
            new[] { "Sanu Vali" },
            new[] { new Vector3(21.0f, 9.6f, -1.4f) },
        };

        yield return new object[]
        {
            "wild hog",
            new[] { "Wild Hog" },
            new[] { new Vector3(41.3f, 7.4f, -0.2f) },
        };
    }
}