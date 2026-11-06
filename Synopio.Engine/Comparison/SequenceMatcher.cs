namespace Synopio.Engine.Comparison;

internal static class SequenceMatcher
{
    // Constants
    private const long MaximumTableSize = 2_500_000;
    private const int MaximumEqualityToDissolve = 2;
    private const int CleanupPasses = 4;
    private const int MaximumSmallRun = 6;
    private const int RegionFactor = 3;

    // Methods
    public static (bool[] LeftMatched, bool[] RightMatched) Match(int[] left, int[] right, int[]? leftGroups = null, int[]? rightGroups = null)
    {
        var pairs = new List<(int Left, int Right)>();
        MatchRange(left, right, 0, left.Length, 0, right.Length, pairs);
        pairs.Sort();

        if (leftGroups != null && rightGroups != null)
        {
            AlignToBoundaries(pairs, left, right, leftGroups, rightGroups);
        }

        var leftMatched = new bool[left.Length];
        var rightMatched = new bool[right.Length];
        foreach (var (leftIndex, rightIndex) in pairs)
        {
            leftMatched[leftIndex] = true;
            rightMatched[rightIndex] = true;
        }

        Cleanup(pairs, leftMatched, rightMatched, left.Length, right.Length);
        return (leftMatched, rightMatched);
    }

    private static void MatchRange(int[] left, int[] right, int leftStart, int leftEnd, int rightStart, int rightEnd, List<(int, int)> pairs)
    {
        while (leftStart < leftEnd && rightStart < rightEnd && left[leftStart] == right[rightStart])
        {
            pairs.Add((leftStart++, rightStart++));
        }

        while (leftStart < leftEnd && rightStart < rightEnd && left[leftEnd - 1] == right[rightEnd - 1])
        {
            pairs.Add((--leftEnd, --rightEnd));
        }

        if (leftStart == leftEnd || rightStart == rightEnd)
        {
            return;
        }

        var anchors = FindUniqueAnchors(left, right, leftStart, leftEnd, rightStart, rightEnd);
        if (anchors.Count > 0)
        {
            var previousLeft = leftStart;
            var previousRight = rightStart;
            foreach (var (anchorLeft, anchorRight) in anchors)
            {
                MatchRange(left, right, previousLeft, anchorLeft, previousRight, anchorRight, pairs);
                pairs.Add((anchorLeft, anchorRight));
                previousLeft = anchorLeft + 1;
                previousRight = anchorRight + 1;
            }

            MatchRange(left, right, previousLeft, leftEnd, previousRight, rightEnd, pairs);
            return;
        }

        if ((long)(leftEnd - leftStart) * (rightEnd - rightStart) <= MaximumTableSize)
        {
            MatchByTable(left, right, leftStart, leftEnd, rightStart, rightEnd, pairs);
        }
    }

    private static List<(int Left, int Right)> FindUniqueAnchors(int[] left, int[] right, int leftStart, int leftEnd, int rightStart, int rightEnd)
    {
        var occurrences = new Dictionary<int, (int LeftCount, int LeftIndex, int RightCount, int RightIndex)>();

        for (var index = leftStart; index < leftEnd; index++)
        {
            var entry = occurrences.GetValueOrDefault(left[index]);
            occurrences[left[index]] = (entry.LeftCount + 1, index, entry.RightCount, entry.RightIndex);
        }

        for (var index = rightStart; index < rightEnd; index++)
        {
            if (occurrences.TryGetValue(right[index], out var entry))
            {
                occurrences[right[index]] = (entry.LeftCount, entry.LeftIndex, entry.RightCount + 1, index);
            }
        }

        var candidates = occurrences.Values
            .Where(entry => entry.LeftCount == 1 && entry.RightCount == 1)
            .Select(entry => (Left: entry.LeftIndex, Right: entry.RightIndex))
            .OrderBy(entry => entry.Left)
            .ToList();

        return LongestIncreasingByRight(candidates);
    }

    private static List<(int Left, int Right)> LongestIncreasingByRight(List<(int Left, int Right)> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var tails = new List<int>();
        var previous = new int[candidates.Count];

        for (var index = 0; index < candidates.Count; index++)
        {
            int low = 0, high = tails.Count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (candidates[tails[middle]].Right < candidates[index].Right)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[index] = low > 0 ? tails[low - 1] : -1;
            if (low == tails.Count)
            {
                tails.Add(index);
            }
            else
            {
                tails[low] = index;
            }
        }

        var result = new List<(int Left, int Right)>(tails.Count);
        for (var index = tails[^1]; index >= 0; index = previous[index])
        {
            result.Add(candidates[index]);
        }

        result.Reverse();
        return result;
    }

    private static void MatchByTable(int[] left, int[] right, int leftStart, int leftEnd, int rightStart, int rightEnd, List<(int, int)> pairs)
    {
        var rows = leftEnd - leftStart;
        var columns = rightEnd - rightStart;
        var table = new int[(rows + 1) * (columns + 1)];

        for (var row = rows - 1; row >= 0; row--)
        {
            for (var column = columns - 1; column >= 0; column--)
            {
                var cell = (row * (columns + 1)) + column;
                table[cell] = left[leftStart + row] == right[rightStart + column]
                    ? table[((row + 1) * (columns + 1)) + column + 1] + 1
                    : Math.Max(table[((row + 1) * (columns + 1)) + column], table[cell + 1]);
            }
        }

        int currentRow = 0, currentColumn = 0;
        while (currentRow < rows && currentColumn < columns)
        {
            if (left[leftStart + currentRow] == right[rightStart + currentColumn])
            {
                pairs.Add((leftStart + currentRow, rightStart + currentColumn));
                currentRow++;
                currentColumn++;
            }
            else if (table[((currentRow + 1) * (columns + 1)) + currentColumn] >= table[(currentRow * (columns + 1)) + currentColumn + 1])
            {
                currentRow++;
            }
            else
            {
                currentColumn++;
            }
        }
    }

    private static void AlignToBoundaries(List<(int Left, int Right)> pairs, int[] left, int[] right, int[] leftGroups, int[] rightGroups)
    {
        for (var index = 0; index <= pairs.Count; index++)
        {
            var leftStart = index == 0 ? 0 : pairs[index - 1].Left + 1;
            var rightStart = index == 0 ? 0 : pairs[index - 1].Right + 1;
            var leftEnd = index == pairs.Count ? left.Length : pairs[index].Left;
            var rightEnd = index == pairs.Count ? right.Length : pairs[index].Right;

            if (leftEnd > leftStart && rightEnd == rightStart)
            {
                ShiftBlock(pairs, index, leftStart, leftEnd, left, leftGroups, isLeft: true);
            }
            else if (rightEnd > rightStart && leftEnd == leftStart)
            {
                ShiftBlock(pairs, index, rightStart, rightEnd, right, rightGroups, isLeft: false);
            }
        }
    }

    private static void ShiftBlock(List<(int Left, int Right)> pairs, int gapIndex, int start, int end, int[] keys, int[] groups, bool isLeft)
    {
        int Side((int Left, int Right) pair) => isLeft ? pair.Left : pair.Right;

        var maximumBackward = 0;
        while (gapIndex - maximumBackward - 1 >= 0
            && Side(pairs[gapIndex - maximumBackward - 1]) == start - maximumBackward - 1
            && keys[start - maximumBackward - 1] == keys[end - maximumBackward - 1])
        {
            maximumBackward++;
        }

        var maximumForward = 0;
        while (gapIndex + maximumForward < pairs.Count
            && Side(pairs[gapIndex + maximumForward]) == end + maximumForward
            && keys[start + maximumForward] == keys[end + maximumForward])
        {
            maximumForward++;
        }

        var bestOffset = 0;
        var bestScore = BoundaryScore(start, end, groups);
        for (var offset = -maximumBackward; offset <= maximumForward; offset++)
        {
            var score = BoundaryScore(start + offset, end + offset, groups);
            if (score > bestScore || (score == bestScore && Math.Abs(offset) < Math.Abs(bestOffset)))
            {
                bestScore = score;
                bestOffset = offset;
            }
        }

        for (var step = 1; step <= -bestOffset; step++)
        {
            var pair = pairs[gapIndex - step];
            pairs[gapIndex - step] = isLeft ? (end - step, pair.Right) : (pair.Left, end - step);
        }

        for (var step = 0; step < bestOffset; step++)
        {
            var pair = pairs[gapIndex + step];
            pairs[gapIndex + step] = isLeft ? (start + step, pair.Right) : (pair.Left, start + step);
        }
    }

    private static int BoundaryScore(int start, int end, int[] groups)
    {
        var score = 0;
        if (start == 0 || groups[start] != groups[start - 1])
        {
            score += 2;
        }

        if (end == groups.Length || groups[end] != groups[end - 1])
        {
            score += 2;
        }

        return score;
    }

    private static void Cleanup(List<(int Left, int Right)> pairs, bool[] leftMatched, bool[] rightMatched, int leftLength, int rightLength)
    {
        for (var pass = 0; pass < CleanupPasses; pass++)
        {
            var runs = BuildRuns(pairs, leftMatched);
            var gaps = new (int Left, int Right)[runs.Count + 1];
            for (var index = 0; index <= runs.Count; index++)
            {
                var previousLeft = index == 0 ? 0 : runs[index - 1].EndLeft;
                var previousRight = index == 0 ? 0 : runs[index - 1].EndRight;
                var nextLeft = index == runs.Count ? leftLength : runs[index].Left;
                var nextRight = index == runs.Count ? rightLength : runs[index].Right;
                gaps[index] = (nextLeft - previousLeft, nextRight - previousRight);
            }

            var dissolve = new List<int>();
            for (var index = 0; index < runs.Count; index++)
            {
                var length = runs[index].Length;
                if (length > MaximumSmallRun)
                {
                    continue;
                }

                var localBefore = gaps[index];
                var localAfter = gaps[index + 1];
                var isLocalNoise = length <= MaximumEqualityToDissolve
                    && length < Math.Max(localBefore.Left, localBefore.Right)
                    && length < Math.Max(localAfter.Left, localAfter.Right)
                    && Math.Min(localBefore.Left, localBefore.Right) + Math.Min(localAfter.Left, localAfter.Right) > 0;

                var before = SumGaps(runs, gaps, index, -1);
                var after = SumGaps(runs, gaps, index + 1, 1);
                var isRegionNoise = length * RegionFactor < Math.Max(before.Left, before.Right)
                    && length * RegionFactor < Math.Max(after.Left, after.Right);

                if (isLocalNoise || isRegionNoise)
                {
                    dissolve.Add(index);
                }
            }

            if (dissolve.Count == 0)
            {
                return;
            }

            foreach (var run in dissolve.Select(index => runs[index]))
            {
                for (var offset = 0; offset < run.Length; offset++)
                {
                    leftMatched[run.Left + offset] = false;
                    rightMatched[run.Right + offset] = false;
                }
            }

            pairs.RemoveAll(pair => !leftMatched[pair.Left]);
        }
    }

    private static (int Left, int Right) SumGaps(List<(int Left, int Right, int Length, int EndLeft, int EndRight)> runs, (int Left, int Right)[] gaps, int gapIndex, int direction)
    {
        var left = 0;
        var right = 0;
        for (var index = gapIndex; index >= 0 && index < gaps.Length; index += direction)
        {
            left += gaps[index].Left;
            right += gaps[index].Right;

            var runIndex = direction < 0 ? index - 1 : index;
            if (runIndex < 0 || runIndex >= runs.Count || runs[runIndex].Length > MaximumSmallRun)
            {
                break;
            }
        }

        return (left, right);
    }

    private static List<(int Left, int Right, int Length, int EndLeft, int EndRight)> BuildRuns(List<(int Left, int Right)> pairs, bool[] leftMatched)
    {
        var runs = new List<(int Left, int Right, int Length, int EndLeft, int EndRight)>();
        foreach (var (left, right) in pairs)
        {
            if (!leftMatched[left])
            {
                continue;
            }

            if (runs.Count > 0 && runs[^1].EndLeft == left && runs[^1].EndRight == right)
            {
                var last = runs[^1];
                runs[^1] = (last.Left, last.Right, last.Length + 1, left + 1, right + 1);
            }
            else
            {
                runs.Add((left, right, 1, left + 1, right + 1));
            }
        }

        return runs;
    }
}
