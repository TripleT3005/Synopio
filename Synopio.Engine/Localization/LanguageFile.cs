namespace Synopio.Engine.Localization;

public sealed class LanguageFile
{
    // Constants
    public const string FileExtension = ".lang";
    public const string EnglishCode = "en";
    public const string NameKey = "language.name";
    public const string FallbackKey = "language.fallback";
    public const string DirectionKey = "language.direction";
    public const string TitlePrefix = "# Synopio language file: ";
    public const string CategoryPrefix = "# Category: ";
    public const string ReviewedByPrefix = "# Reviewed by: ";
    public const string StandardCategory = "standard";

    // Properties
    public required string Code { get; init; }
    public required IReadOnlyDictionary<string, string> Entries { get; init; }
    public string? Title { get; init; }
    public string Category { get; init; } = StandardCategory;
    public IReadOnlyList<string> Reviewers { get; init; } = [];

    public string Name => Entries.GetValueOrDefault(NameKey, Code);
    public string EnglishName => Title is { } title && title.EndsWith($"({Code})", StringComparison.Ordinal) ? title[..^(Code.Length + 2)].Trim() : Name;
    public string? Fallback => Entries.GetValueOrDefault(FallbackKey) is { Length: > 0 } fallback ? fallback : null;
    public bool IsRightToLeft => string.Equals(Entries.GetValueOrDefault(DirectionKey), "rtl", StringComparison.OrdinalIgnoreCase);
    public bool IsStandard => string.Equals(Category, StandardCategory, StringComparison.OrdinalIgnoreCase);

    // Methods
    public static LanguageFile Parse(string code, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        string? title = null;
        var category = StandardCategory;
        var reviewers = new List<string>();

        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                if (line.StartsWith(TitlePrefix, StringComparison.Ordinal))
                {
                    title = line[TitlePrefix.Length..].Trim();
                }
                else if (line.StartsWith(CategoryPrefix, StringComparison.Ordinal))
                {
                    category = line[CategoryPrefix.Length..].Trim();
                }
                else if (line.StartsWith(ReviewedByPrefix, StringComparison.Ordinal))
                {
                    reviewers.AddRange(line[ReviewedByPrefix.Length..]
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Where(reviewer => reviewer != "-")
                        .Select(reviewer => reviewer.TrimStart('@')));
                }

                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim()
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal);
            entries[key] = value;
        }

        return new LanguageFile { Code = code, Entries = entries, Title = title, Category = category, Reviewers = reviewers };
    }

    public static LanguageFile Load(string path) =>
        Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
}
