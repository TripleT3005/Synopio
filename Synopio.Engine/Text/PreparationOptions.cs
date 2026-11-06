namespace Synopio.Engine.Text;

public sealed record PreparationOptions
{
    // Properties
    public int? FirstPage { get; init; }
    public int? LastPage { get; init; }
    public bool RemoveHeadersAndFooters { get; init; } = true;
    public bool RepairLigatures { get; init; } = true;
    public bool JoinHyphenatedWords { get; init; } = true;
}
