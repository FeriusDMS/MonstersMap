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

        // Compute normalized names once, require that all search terms match,
        // then sort by earliest match position (terms found earlier are more relevant),
        // then by number of matched terms (desc), then by name length (shorter first).
        var prepared = candidates
            .Select(c => new
            {
                Candidate = c,
                Normalized = Normalize(c.Name)
            })
            .Select(x => new
            {
                x.Candidate,
                x.Normalized,
                Indices = searchTerms.Select(t => x.Normalized.IndexOf(t, StringComparison.OrdinalIgnoreCase)).ToArray()
            })
            .Select(x => new
            {
                x.Candidate,
                MatchCount = x.Indices.Count(i => i >= 0),
                FirstIndex = x.Indices.Where(i => i >= 0).DefaultIfEmpty(int.MaxValue).Min(),
            })
            .Where(x => x.MatchCount == searchTerms.Length)
            .OrderBy(x => x.FirstIndex)
            .ThenByDescending(x => x.MatchCount)
            .ThenBy(x => x.Candidate.Name.Length)
            .ThenBy(x => x.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Candidate)
            .ToArray();

        return prepared;
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