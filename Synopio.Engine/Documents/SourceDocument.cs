namespace Synopio.Engine.Documents;

public sealed class SourceDocument
{
    // Properties
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required bool HasLayout { get; init; }
    public required IReadOnlyList<SourceLine> Lines { get; init; }

    public int PageCount => Lines.Count == 0 ? 0 : Lines.Max(line => line.PageNumber);
}
