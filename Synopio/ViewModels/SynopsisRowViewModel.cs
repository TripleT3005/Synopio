using Synopio.Engine.Comparison;
using Synopio.Localization;

namespace Synopio.ViewModels;

public sealed class SynopsisRowViewModel
{
    // Constructor
    public SynopsisRowViewModel(SynopsisRow row, int number)
    {
        Row = row;
        Number = number;
    }

    // Properties
    public SynopsisRow Row { get; }

    public int Number { get; }

    public IReadOnlyList<Segment> Left => Row.Left;

    public IReadOnlyList<Segment> Right => Row.Right;

    public RowKind Kind => Row.Kind;

    public bool HasChanges => Row.HasChanges;

    public string LeftPage => Row.LeftPage is { } page ? Localizer.Instance.Format("synopsis.page", page) : string.Empty;

    public string RightPage => Row.RightPage is { } page ? Localizer.Instance.Format("synopsis.page", page) : string.Empty;

    public string PlainText =>
        string.Concat(Left.Where(segment => segment.Kind != SegmentKind.Inserted).Select(segment => segment.Text)) + "\t" +
        string.Concat(Right.Where(segment => segment.Kind != SegmentKind.Deleted).Select(segment => segment.Text));
}
