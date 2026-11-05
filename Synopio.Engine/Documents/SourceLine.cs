namespace Synopio.Engine.Documents;

public sealed record SourceLine(string Text, int PageNumber)
{
    // Properties
    public double Top { get; init; }
    public double Bottom { get; init; }
    public double Left { get; init; }
    public double Right { get; init; }
    public double FontSize { get; init; }
    public bool StartsParagraph { get; init; }

    public double Height => Bottom - Top;
}
