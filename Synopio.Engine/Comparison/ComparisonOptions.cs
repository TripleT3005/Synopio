namespace Synopio.Engine.Comparison;

public sealed record ComparisonOptions
{
    // Properties
    public bool IgnoreCase { get; init; }
    public bool IgnoreHyphens { get; init; } = true;
    public bool IgnorePunctuation { get; init; }
}
