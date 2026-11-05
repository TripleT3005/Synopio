using System.Text;
using Synopio.Engine.Text;

namespace Synopio.Engine.Comparison;

public static class SynopsisBuilder
{
    // Constants
    private const int UnlinkedSearchWindow = 4;
    private const double MinimumParagraphSimilarity = 0.3;

    // Methods
    public static Synopsis Compare(IReadOnlyList<Paragraph> left, IReadOnlyList<Paragraph> right, ComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(options);

        var leftTokens = Tokenizer.Tokenize(left, options);
        var rightTokens = Tokenizer.Tokenize(right, options);
        var (leftMatched, rightMatched) = MatchTokens(leftTokens, rightTokens, options);

        var rows = BuildRows(left, right, leftTokens, rightTokens, leftMatched, rightMatched);
        return new Synopsis(rows, CreateStatistics(rows, leftTokens, rightTokens, leftMatched, rightMatched));
    }

    private static (bool[] LeftMatched, bool[] RightMatched) MatchTokens(List<Token> leftTokens, List<Token> rightTokens, ComparisonOptions options)
    {
        var dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
        var leftIndexes = ComparableIndexes(leftTokens, options);
        var rightIndexes = ComparableIndexes(rightTokens, options);

        var leftKeys = leftIndexes.Select(index => KeyId(leftTokens[index].Key)).ToArray();
        var rightKeys = rightIndexes.Select(index => KeyId(rightTokens[index].Key)).ToArray();
        var (leftComparableMatched, rightComparableMatched) = SequenceMatcher.Match(leftKeys, rightKeys,
            [.. leftIndexes.Select(index => leftTokens[index].ParagraphIndex)],
            [.. rightIndexes.Select(index => rightTokens[index].ParagraphIndex)]);

        var leftMatched = new bool[leftTokens.Count];
        var rightMatched = new bool[rightTokens.Count];
        Expand(leftMatched, leftIndexes, leftComparableMatched, leftTokens, options);
        Expand(rightMatched, rightIndexes, rightComparableMatched, rightTokens, options);
        return (leftMatched, rightMatched);

        int KeyId(string key)
        {
            if (!dictionary.TryGetValue(key, out var id))
            {
                id = dictionary.Count;
                dictionary[key] = id;
            }

            return id;
        }
    }

    private static List<int> ComparableIndexes(List<Token> tokens, ComparisonOptions options) =>
        [.. Enumerable.Range(0, tokens.Count).Where(index => !options.IgnorePunctuation || !tokens[index].IsPunctuation)];

    private static void Expand(bool[] matched, List<int> indexes, bool[] comparableMatched, List<Token> tokens, ComparisonOptions options)
    {
        for (var index = 0; index < indexes.Count; index++)
        {
            matched[indexes[index]] = comparableMatched[index];
        }

        if (!options.IgnorePunctuation)
        {
            return;
        }

        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].IsPunctuation)
            {
                matched[index] = true;
            }
        }
    }

    private static List<SynopsisRow> BuildRows(IReadOnlyList<Paragraph> left, IReadOnlyList<Paragraph> right,
        List<Token> leftTokens, List<Token> rightTokens, bool[] leftMatched, bool[] rightMatched)
    {
        var links = LinkParagraphs(leftTokens, rightTokens, leftMatched, rightMatched);
        var leftByParagraph = GroupByParagraph(leftTokens, leftMatched, left.Count);
        var rightByParagraph = GroupByParagraph(rightTokens, rightMatched, right.Count);
        var groups = new List<(int LeftStart, int LeftEnd, int RightStart, int RightEnd)>();
        var leftNext = 0;
        var rightNext = 0;

        foreach (var (leftParagraph, rightParagraph) in links)
        {
            if (groups.Count > 0 && (leftParagraph < groups[^1].LeftEnd || rightParagraph < groups[^1].RightEnd))
            {
                var last = groups[^1];
                groups[^1] = (last.LeftStart, Math.Max(last.LeftEnd, leftParagraph + 1), last.RightStart, Math.Max(last.RightEnd, rightParagraph + 1));
                leftNext = groups[^1].LeftEnd;
                rightNext = groups[^1].RightEnd;
                continue;
            }

            AddUnlinked(groups, leftByParagraph, rightByParagraph, leftNext, leftParagraph, rightNext, rightParagraph);
            groups.Add((leftParagraph, leftParagraph + 1, rightParagraph, rightParagraph + 1));
            leftNext = leftParagraph + 1;
            rightNext = rightParagraph + 1;
        }

        AddUnlinked(groups, leftByParagraph, rightByParagraph, leftNext, left.Count, rightNext, right.Count);

        return [.. groups.Select(group => CreateRow(group, left, right, leftByParagraph, rightByParagraph))];
    }

    private static List<(int Left, int Right)> LinkParagraphs(List<Token> leftTokens, List<Token> rightTokens, bool[] leftMatched, bool[] rightMatched)
    {
        var links = new List<(int Left, int Right)>();
        var rightIndex = 0;

        for (var leftIndex = 0; leftIndex < leftTokens.Count; leftIndex++)
        {
            if (!leftMatched[leftIndex] || leftTokens[leftIndex].IsPunctuation)
            {
                continue;
            }

            while (rightIndex < rightTokens.Count && (!rightMatched[rightIndex] || rightTokens[rightIndex].IsPunctuation))
            {
                rightIndex++;
            }

            if (rightIndex >= rightTokens.Count)
            {
                break;
            }

            var link = (leftTokens[leftIndex].ParagraphIndex, rightTokens[rightIndex].ParagraphIndex);
            if (links.Count == 0 || links[^1] != link)
            {
                links.Add(link);
            }

            rightIndex++;
        }

        return links;
    }

    private static void AddUnlinked(List<(int LeftStart, int LeftEnd, int RightStart, int RightEnd)> groups,
        List<List<(Token Token, bool Matched)>> leftByParagraph, List<List<(Token Token, bool Matched)>> rightByParagraph,
        int leftStart, int leftEnd, int rightStart, int rightEnd)
    {
        var rightNext = rightStart;
        for (var leftIndex = leftStart; leftIndex < leftEnd; leftIndex++)
        {
            var leftWords = WordKeys(leftByParagraph[leftIndex]);
            var match = Enumerable.Range(rightNext, Math.Max(0, Math.Min(UnlinkedSearchWindow, rightEnd - rightNext)))
                .FirstOrDefault(rightIndex => Similarity(leftWords, WordKeys(rightByParagraph[rightIndex])) >= MinimumParagraphSimilarity, -1);

            if (match < 0)
            {
                groups.Add((leftIndex, leftIndex + 1, rightNext, rightNext));
                continue;
            }

            for (var rightIndex = rightNext; rightIndex < match; rightIndex++)
            {
                groups.Add((leftIndex, leftIndex, rightIndex, rightIndex + 1));
            }

            groups.Add((leftIndex, leftIndex + 1, match, match + 1));
            rightNext = match + 1;
        }

        for (var rightIndex = rightNext; rightIndex < rightEnd; rightIndex++)
        {
            groups.Add((leftEnd, leftEnd, rightIndex, rightIndex + 1));
        }
    }

    private static HashSet<string> WordKeys(List<(Token Token, bool Matched)> paragraph) =>
        [.. paragraph.Where(entry => !entry.Token.IsPunctuation).Select(entry => entry.Token.Key)];

    private static double Similarity(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = left.Count(right.Contains);
        return (double)shared / (left.Count + right.Count - shared);
    }

    private static List<List<(Token Token, bool Matched)>> GroupByParagraph(List<Token> tokens, bool[] matched, int paragraphCount)
    {
        var result = Enumerable.Range(0, paragraphCount).Select(_ => new List<(Token, bool)>()).ToList();
        for (var index = 0; index < tokens.Count; index++)
        {
            result[tokens[index].ParagraphIndex].Add((tokens[index], matched[index]));
        }

        return result;
    }

    private static SynopsisRow CreateRow((int LeftStart, int LeftEnd, int RightStart, int RightEnd) group,
        IReadOnlyList<Paragraph> left, IReadOnlyList<Paragraph> right,
        List<List<(Token Token, bool Matched)>> leftByParagraph, List<List<(Token Token, bool Matched)>> rightByParagraph)
    {
        var leftSegments = CreateSegments(leftByParagraph, group.LeftStart, group.LeftEnd, SegmentKind.Deleted);
        var rightSegments = CreateSegments(rightByParagraph, group.RightStart, group.RightEnd, SegmentKind.Inserted);

        var leftEmpty = group.LeftStart == group.LeftEnd;
        var rightEmpty = group.RightStart == group.RightEnd;
        var kind = leftEmpty ? RowKind.Inserted
            : rightEmpty ? RowKind.Deleted
            : leftSegments.Concat(rightSegments).Any(segment => segment.Kind is SegmentKind.Deleted or SegmentKind.Inserted) ? RowKind.Modified
            : RowKind.Unchanged;

        return new SynopsisRow(leftSegments, rightSegments, kind,
            leftEmpty ? null : left[group.LeftStart].PageNumber,
            rightEmpty ? null : right[group.RightStart].PageNumber);
    }

    private static List<Segment> CreateSegments(List<List<(Token Token, bool Matched)>> paragraphs, int start, int end, SegmentKind changedKind)
    {
        var segments = new List<Segment>();
        var builder = new StringBuilder();
        var currentKind = SegmentKind.Equal;

        for (var paragraph = start; paragraph < end; paragraph++)
        {
            if (paragraph > start)
            {
                Flush();
                segments.Add(new Segment("\n", SegmentKind.ParagraphBreak));
            }

            var first = true;
            foreach (var (token, matched) in paragraphs[paragraph])
            {
                var kind = matched ? SegmentKind.Equal : changedKind;
                var space = token.HasLeadingSpace && !first ? " " : string.Empty;

                if (kind != currentKind)
                {
                    if (kind == SegmentKind.Equal)
                    {
                        Flush();
                        builder.Append(space);
                    }
                    else
                    {
                        builder.Append(space);
                        Flush();
                    }

                    currentKind = kind;
                    builder.Append(token.Text);
                }
                else
                {
                    builder.Append(space).Append(token.Text);
                }

                first = false;
            }
        }

        Flush();
        return segments;

        void Flush()
        {
            if (builder.Length > 0)
            {
                segments.Add(new Segment(builder.ToString(), currentKind));
                builder.Clear();
            }
        }
    }

    private static SynopsisStatistics CreateStatistics(List<SynopsisRow> rows, List<Token> leftTokens, List<Token> rightTokens, bool[] leftMatched, bool[] rightMatched) =>
        new(
            rows.Count(row => row.Kind == RowKind.Unchanged),
            rows.Count(row => row.Kind == RowKind.Modified),
            rows.Count(row => row.Kind == RowKind.Deleted),
            rows.Count(row => row.Kind == RowKind.Inserted),
            leftTokens.Where((token, index) => !leftMatched[index] && !token.IsPunctuation).Count(),
            rightTokens.Where((token, index) => !rightMatched[index] && !token.IsPunctuation).Count());
}
