using Synopio.Engine.Comparison;

namespace Synopio.Engine.Projects;

public sealed record SynopioProject
{
    // Constants
    public const int CurrentFormatVersion = 1;
    public const string FileExtension = ".synopio";

    // Properties
    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public ProjectDocument? Left { get; init; }
    public ProjectDocument? Right { get; init; }
    public ComparisonOptions Comparison { get; init; } = new();
}

public sealed record ProjectDocument
{
    // Properties
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public int? FirstPage { get; init; }
    public int? LastPage { get; init; }
    public bool RemoveHeadersAndFooters { get; init; } = true;
    public bool RepairLigatures { get; init; } = true;
    public string? EditedText { get; init; }
}
