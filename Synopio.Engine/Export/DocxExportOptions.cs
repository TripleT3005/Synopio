namespace Synopio.Engine.Export;

public sealed record DocxExportOptions
{
    // Properties
    public required string Title { get; init; }
    public required string LeftHeading { get; init; }
    public required string RightHeading { get; init; }
    public required string PageLabel { get; init; }
    public bool OnlyChangedRows { get; init; }
    public bool ShowPageNumbers { get; init; } = true;
    public string? FooterText { get; init; }
}
