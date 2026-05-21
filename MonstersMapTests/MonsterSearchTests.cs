using System.Collections.Generic;

using MonstersMap;

using Xunit;

namespace MonstersMapTests;

public class MonsterSearchTests
{
    [Fact]
    public void Finds_MultiWord_Names()
    {
        var candidates = MonsterSampleData.BuildCandidates("Muu Shuwuu", "Stormslime", "imp");

        var results = MonsterSearch.FindMatches(candidates, "Muu Shuwuu");

        Assert.Single(results);
        Assert.Equal("Muu Shuwuu", results[0].Name);
        Assert.Equal(candidates[0].Position, results[0].Position);
    }

    [Fact]
    public void Finds_Partial_Names()
    {
        var candidates = MonsterSampleData.BuildCandidates("Muu Shuwuu", "Baalzephon");

        var results = MonsterSearch.FindMatches(candidates, "muu");

        Assert.Single(results);
        Assert.Equal("Muu Shuwuu", results[0].Name);
        Assert.Equal(candidates[0].Position, results[0].Position);
    }

    [Theory]
    [MemberData(nameof(MultiMatchCases))]
    public void Supports_Varied_Search_Scenarios(
        string query,
        string[] expectedNames)
    {
        var candidates = MonsterSampleData.BuildCandidates(
            "Muu Shuwuu",
            "Sanu Vali",
            "Baalzephon",
            "Stormslime",
            "Wild Hog",
            "imp");

        var results = MonsterSearch.FindMatches(candidates, query);

        Assert.Equal(expectedNames.Length, results.Count);

        for (var index = 0; index < expectedNames.Length; index++)
        {
            Assert.Equal(expectedNames[index], results[index].Name);
            Assert.Equal(candidates[index].Position, results[index].Position);
        }
    }

    [Fact]
    public void Ignores_Accent_And_Whitespace_Noise()
    {
        var candidates = MonsterSampleData.BuildCandidates("Vautour");

        var results = MonsterSearch.FindMatches(candidates, "  Vautoúr  ");

        Assert.Single(results);
        Assert.Equal("Vautour", results[0].Name);
        Assert.Equal(candidates[0].Position, results[0].Position);
    }

    [Fact]
    public void Returns_Multiple_Matches_When_Query_Is_Generic()
    {
        var candidates = MonsterSampleData.BuildCandidates("Muu Shuwuu", "Wild Hog", "Stormslime", "imp");

        var results = MonsterSearch.FindMatches(candidates, "w");

        Assert.Equal(2, results.Count);
        Assert.Equal("Wild Hog", results[0].Name);
        Assert.Equal("Muu Shuwuu", results[1].Name);
    }

    [Fact]
    public void Returns_Empty_For_No_Match()
    {
        var candidates = MonsterSampleData.BuildCandidates("Muu Shuwuu");

        var results = MonsterSearch.FindMatches(candidates, "not-a-real-monster");

        Assert.Empty(results);
    }

    public static IEnumerable<object[]> MultiMatchCases()
    {
        yield return new object[]
        {
            "Muu Shuwuu",
            new[] { "Muu Shuwuu" },
        };

        yield return new object[]
        {
            "muu",
            new[] { "Muu Shuwuu" },
        };

        yield return new object[]
        {
            "sanu vali",
            new[] { "Sanu Vali" },
        };

        yield return new object[]
        {
            "wild hog",
            new[] { "Wild Hog" },
        };

        yield return new object[]
        {
            "imp",
            new[] { "imp" },
        };
    }
}