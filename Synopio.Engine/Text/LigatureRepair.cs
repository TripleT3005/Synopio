using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Synopio.Engine.Text;

internal static partial class LigatureRepair
{
    // Constants
    private const int MinimumVotes = 3;
    private const double MinimumVoteShare = 0.8;
    private const string CommonPunctuation = ",;:!?()[]{}\"„“”«»‚‘…*%§$€+";
    private const char UnknownCharacter = '\uFFFD';

    // Fields
    private static readonly string[] CombiningMarks = ["\u0308", "\u0301", "\u0300", "\u0302"];
    private static readonly string[] Candidates = ["ti", "tt", "ft", "tf", "fi", "fl", "ff", "ffi", "ffl", "fj", "tl", "th", "st"];

    // Methods
    public static List<string> Apply(IReadOnlyList<string> lines, IEnumerable<string>? referenceLines = null)
    {
        var reference = referenceLines?.ToList() ?? [];
        var vocabulary = BuildVocabulary(lines.Concat(reference));
        var votes = new Dictionary<char, Dictionary<string, int>>();
        var repaired = lines.Select(line => SuspiciousWordPattern().Replace(line, match => ResolveByVocabulary(match.Value, vocabulary, votes))).ToList();

        var mapping = votes
            .Select(vote => (Glyph: vote.Key, Best: vote.Value.MaxBy(pair => pair.Value), Total: vote.Value.Values.Sum()))
            .Where(vote => vote.Best.Value >= MinimumVotes && vote.Best.Value >= vote.Total * MinimumVoteShare)
            .ToDictionary(vote => vote.Glyph, vote => vote.Best.Key);

        if (mapping.Count > 0)
        {
            repaired = [.. repaired.Select(line => SuspiciousWordPattern().Replace(line, match => ApplyMapping(match.Value, mapping)))];
        }

        vocabulary = BuildVocabulary(repaired.Concat(reference));
        repaired = [.. repaired.Select(line => SuspiciousWordPattern().Replace(line, match => ResolveByVocabulary(match.Value, vocabulary, votes)))];
        return [.. repaired.Select(line => JoinSplitWords(line, vocabulary))];
    }

    private static string ResolveByVocabulary(string word, Dictionary<string, int> vocabulary, Dictionary<char, Dictionary<string, int>> votes)
    {
        var positions = SuspiciousPositions(word, lenient: true).ToList();
        if (positions.Count != 1)
        {
            return word;
        }

        var position = positions[0];
        var options = Candidates
            .Select(candidate => (Candidate: candidate, Word: string.Concat(word.AsSpan(0, position), candidate, word.AsSpan(position + 1)), Position: position))
            .ToList();

        if (word[position] == UnknownCharacter)
        {
            options.AddRange(CombiningMarks.Select(mark =>
                (Candidate: string.Empty, Word: string.Concat(word.AsSpan(0, position), mark, word.AsSpan(position + 1)).Normalize(NormalizationForm.FormC), Position: position - 1)));
        }

        var best = options
            .Select(option => (option.Candidate, option.Word, Count: vocabulary.GetValueOrDefault(LetterRunAt(option.Word, Math.Max(0, option.Position)).ToLowerInvariant())))
            .Where(option => option.Count > 0)
            .OrderByDescending(option => option.Count)
            .FirstOrDefault();

        if (best.Word == null)
        {
            return word;
        }

        if (best.Candidate.Length == 0)
        {
            return best.Word;
        }

        var glyph = word[position];
        if (!votes.TryGetValue(glyph, out var glyphVotes))
        {
            glyphVotes = [];
            votes[glyph] = glyphVotes;
        }

        glyphVotes[best.Candidate] = glyphVotes.GetValueOrDefault(best.Candidate) + 1;
        return best.Word;
    }

    private static string LetterRunAt(string text, int position)
    {
        var start = position;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        var end = position;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        return text[start..end];
    }

    private static string ApplyMapping(string word, Dictionary<char, string> mapping)
    {
        var positions = SuspiciousPositions(word, lenient: false).ToHashSet();
        if (positions.Count == 0 || positions.Any(position => !mapping.ContainsKey(word[position])))
        {
            return word;
        }

        var builder = new StringBuilder(word.Length + positions.Count);
        for (var index = 0; index < word.Length; index++)
        {
            builder.Append(positions.Contains(index) ? mapping[word[index]] : word[index].ToString());
        }

        return builder.ToString();
    }

    private static string JoinSplitWords(string line, Dictionary<string, int> vocabulary) =>
        SplitWordPattern().Replace(line, match =>
        {
            var head = match.Groups["head"].Value;
            var tail = match.Groups["tail"].Value;
            if (vocabulary.GetValueOrDefault(head.ToLowerInvariant()) >= 2 || vocabulary.GetValueOrDefault(tail.ToLowerInvariant()) >= 2)
            {
                return match.Value;
            }

            foreach (var candidate in Candidates)
            {
                var joined = head + candidate + tail;
                if (vocabulary.ContainsKey(joined.ToLowerInvariant()))
                {
                    return joined;
                }
            }

            return match.Value;
        });

    private static IEnumerable<int> SuspiciousPositions(string word, bool lenient)
    {
        for (var index = 1; index < word.Length; index++)
        {
            var current = word[index];
            var previous = word[index - 1];
            var hasNext = index + 1 < word.Length;
            var next = hasNext ? word[index + 1] : '\0';
            var atWordEnd = !hasNext || !char.IsLetterOrDigit(next);

            if (char.IsUpper(current) && char.IsLower(previous) && hasNext && char.IsLower(next))
            {
                yield return index;
                continue;
            }

            if (char.IsLetter(current) || !char.IsLetter(previous) || current is '-' or '\x27' or '’' or '.' or '/' or '&')
            {
                continue;
            }

            var needsLetterAfter = char.IsDigit(current) || CommonPunctuation.Contains(current, StringComparison.Ordinal);
            if (char.IsLower(next) || (atWordEnd && (lenient || !needsLetterAfter)))
            {
                yield return index;
            }
        }
    }

    private static Dictionary<string, int> BuildVocabulary(IEnumerable<string> lines)
    {
        var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            foreach (Match match in WordPattern().Matches(line))
            {
                if (match.Length < 3)
                {
                    continue;
                }

                var key = match.Value.ToLower(CultureInfo.InvariantCulture);
                vocabulary[key] = vocabulary.GetValueOrDefault(key) + 1;
            }
        }

        return vocabulary;
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\p{L}+(?:[^\s\p{L}\-'’./&]\p{L}*)+|(?<![\p{L}\p{N}])\p{L}*\p{Ll}\p{Lu}\p{Ll}+")]
    private static partial Regex SuspiciousWordPattern();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\p{L}+(?![\p{L}\p{N}])")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?<head>\p{L}{2,}) (?<tail>\p{Ll}{2,})(?![\p{L}\p{N}])")]
    private static partial Regex SplitWordPattern();
}
