namespace Synopio.Engine.Comparison;

public enum SegmentKind
{
    Equal,
    Deleted,
    Inserted,
    ParagraphBreak,
}

public enum RowKind
{
    Unchanged,
    Modified,
    Deleted,
    Inserted,
}

public sealed record Segment(string Text, SegmentKind Kind);

public sealed record SynopsisRow(IReadOnlyList<Segment> Left, IReadOnlyList<Segment> Right, RowKind Kind, int? LeftPage, int? RightPage)
{
    // Properties
    public bool HasChanges => Kind != RowKind.Unchanged;
}

public sealed record SynopsisStatistics(int UnchangedRows, int ModifiedRows, int DeletedRows, int InsertedRows, int DeletedWords, int InsertedWords);

public sealed record Synopsis(IReadOnlyList<SynopsisRow> Rows, SynopsisStatistics Statistics);
