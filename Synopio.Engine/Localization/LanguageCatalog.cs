namespace Synopio.Engine.Localization;

public sealed class LanguageCatalog
{
    // Fields
    private readonly Dictionary<string, LanguageFile> _languages;

    // Constructor
    private LanguageCatalog(Dictionary<string, LanguageFile> languages)
    {
        _languages = languages;
    }

    // Properties
    public IReadOnlyList<LanguageFile> Languages =>
        [.. _languages.Values.OrderBy(language => language.Name, StringComparer.CurrentCultureIgnoreCase)];

    // Methods
    public static LanguageCatalog Load(string englishContent, IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        var languages = new Dictionary<string, LanguageFile>(StringComparer.OrdinalIgnoreCase)
        {
            [LanguageFile.EnglishCode] = LanguageFile.Parse(LanguageFile.EnglishCode, englishContent),
        };

        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*" + LanguageFile.FileExtension).Order(StringComparer.Ordinal))
            {
                var file = LanguageFile.Load(path);
                if (!IsValidCode(file.Code))
                {
                    continue;
                }

                languages[file.Code] = languages.TryGetValue(file.Code, out var existing) ? Overlay(existing, file) : file;
            }
        }

        return new LanguageCatalog(languages);
    }

    public LanguageFile? Find(string code) => _languages.GetValueOrDefault(code);

    public LanguageFile Resolve(string? preference, IEnumerable<string> preferredUiLanguages)
    {
        ArgumentNullException.ThrowIfNull(preferredUiLanguages);

        if (preference != null && _languages.TryGetValue(preference, out var chosen))
        {
            return chosen;
        }

        foreach (var code in preferredUiLanguages)
        {
            foreach (var candidate in Candidates(code))
            {
                if (_languages.TryGetValue(candidate, out var language) && language.IsStandard)
                {
                    return language;
                }
            }
        }

        return _languages[LanguageFile.EnglishCode];
    }

    public IReadOnlyList<LanguageFile> Chain(LanguageFile language)
    {
        ArgumentNullException.ThrowIfNull(language);

        var chain = new List<LanguageFile>();
        var codes = new List<string> { language.Code };
        if (language.Fallback is { } fallback)
        {
            codes.AddRange(Candidates(fallback));
        }

        codes.AddRange(Candidates(language.Code).Skip(1));
        codes.Add(LanguageFile.EnglishCode);

        foreach (var code in codes)
        {
            if (_languages.TryGetValue(code, out var file) && !chain.Contains(file))
            {
                chain.Add(file);
            }
        }

        return chain;
    }

    private static IEnumerable<string> Candidates(string code)
    {
        var current = code;
        while (current.Length > 0)
        {
            yield return current;
            var separator = current.LastIndexOf('-');
            if (separator <= 0)
            {
                yield break;
            }

            current = current[..separator];
        }
    }

    private static LanguageFile Overlay(LanguageFile existing, LanguageFile overlay)
    {
        var entries = new Dictionary<string, string>(existing.Entries, StringComparer.Ordinal);
        foreach (var (key, value) in overlay.Entries)
        {
            entries[key] = value;
        }

        return new LanguageFile
        {
            Code = existing.Code,
            Entries = entries,
            Title = existing.Title ?? overlay.Title,
            Category = existing.Category,
            Reviewers = existing.Reviewers,
        };
    }

    private static bool IsValidCode(string code) =>
        code.Length is > 0 and <= 35 && code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
