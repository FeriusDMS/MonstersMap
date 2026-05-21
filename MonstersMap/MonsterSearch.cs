using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace MonstersMap;

public sealed record MonsterSearchCandidate(string Name, Vector3 Position);

public static class MonsterSearch
{
    public static IReadOnlyList<MonsterSearchCandidate> FindMatches(
        IEnumerable<MonsterSearchCandidate> candidates,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Array.Empty<MonsterSearchCandidate>();
        }

        var searchTerms = Tokenize(searchText);
        if (searchTerms.Length == 0)
        {
            return Array.Empty<MonsterSearchCandidate>();
        }

        return candidates
            .Where(candidate => Matches(candidate.Name, searchTerms))
            .ToArray();
    }

    internal static bool Matches(string candidateName, string[] searchTerms)
    {
        var normalizedCandidate = Normalize(candidateName);
        return searchTerms.All(term => normalizedCandidate.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    internal static string Normalize(string text)
    {
        var normalized = text
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToLowerInvariant()));
    }

    private static string[] Tokenize(string text)
    {
        return Normalize(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}