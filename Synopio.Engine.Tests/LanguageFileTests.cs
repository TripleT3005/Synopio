using System.Text.RegularExpressions;
using Synopio.Engine.Localization;

namespace Synopio.Engine.Tests;

public partial class LanguageFileTests
{
    // Fields
    private static readonly string[] Categories = ["standard", "dialect", "fun"];

    // Helpers
    private static List<LanguageFile> LoadLanguages() =>
        [.. Directory.EnumerateFiles(TestPaths.LanguageDirectory, "*" + LanguageFile.FileExtension).Select(LanguageFile.Load)];

    private static LanguageFile English => LanguageFile.Load(Path.Combine(TestPaths.LanguageDirectory, "en.lang"));

    private static LanguageCatalog CreateCatalog(params (string Code, string Content)[] files)
    {
        var directory = Directory.CreateTempSubdirectory("synopio-lang-");
        foreach (var (code, content) in files)
        {
            File.WriteAllText(Path.Combine(directory.FullName, code + LanguageFile.FileExtension), content);
        }

        var catalog = LanguageCatalog.Load("language.name = English\ngreeting = Hello\nfarewell = Bye\n", [directory.FullName]);
        directory.Delete(recursive: true);
        return catalog;
    }

    // Tests
    [Fact]
    public void ParserReadsHeaderKeysValuesAndEscapes()
    {
        var file = LanguageFile.Parse("de-x-test", "# Synopio language file: Test German (de-x-test)\n# Category: fun\n# Reviewed by: @Someone, @Other\nlanguage.name = Test\nkey.one = Line one\\nLine two\n broken line\nkey.two = a = b\n");

        Assert.Equal("Test", file.Name);
        Assert.Equal("Test German", file.EnglishName);
        Assert.Equal("fun", file.Category);
        Assert.Equal(["Someone", "Other"], file.Reviewers);
        Assert.Equal("Line one\nLine two", file.Entries["key.one"]);
        Assert.Equal("a = b", file.Entries["key.two"]);
        Assert.Equal(3, file.Entries.Count);
    }

    [Fact]
    public void EveryLanguageFileHasHeader()
    {
        foreach (var language in LoadLanguages())
        {
            Assert.True(language.Title?.EndsWith($"({language.Code})", StringComparison.Ordinal) == true, $"{language.Code}: title");
            Assert.Contains(language.Category, Categories);
            Assert.True(language.Entries.ContainsKey(LanguageFile.NameKey), $"{language.Code}: {LanguageFile.NameKey}");
        }
    }

    [Fact]
    public void LanguageFilesOnlyUseKnownKeys()
    {
        var englishKeys = English.Entries.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in LoadLanguages())
        {
            var unknown = language.Entries.Keys
                .Where(key => !englishKeys.Contains(key) && key is not LanguageFile.FallbackKey and not LanguageFile.DirectionKey)
                .ToList();
            Assert.True(unknown.Count == 0, $"{language.Code}: {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void PlaceholdersMatchEnglish()
    {
        var english = English;
        foreach (var language in LoadLanguages())
        {
            foreach (var (key, value) in language.Entries.Where(entry => english.Entries.ContainsKey(entry.Key)))
            {
                Assert.True(Placeholders(english.Entries[key]).SetEquals(Placeholders(value)), $"{language.Code}: {key}");
            }
        }
    }

    [Fact]
    public void CatalogFollowsWindowsLanguageAndFallsBackToBaseLanguage()
    {
        var catalog = CreateCatalog(("de", "# Category: standard\nlanguage.name = Deutsch\ngreeting = Hallo\n"));

        Assert.Equal("de", catalog.Resolve(null, ["de-AT", "de"]).Code);
        Assert.Equal("en", catalog.Resolve(null, ["fr-FR", "fr"]).Code);
        Assert.Equal("de", catalog.Resolve("de", ["en-US"]).Code);
    }

    [Fact]
    public void CatalogNeverChoosesDialectsAutomatically()
    {
        var catalog = CreateCatalog(("bar", "# Category: dialect\nlanguage.name = Boarisch\nlanguage.fallback = de\n"));

        Assert.Equal("en", catalog.Resolve(null, ["bar"]).Code);
        Assert.Equal("bar", catalog.Resolve("bar", ["en-US"]).Code);
    }

    [Fact]
    public void MissingTextsFallBackThroughDeclaredLanguage()
    {
        var catalog = CreateCatalog(
            ("de", "# Category: standard\nlanguage.name = Deutsch\ngreeting = Hallo\n"),
            ("bar", "# Category: dialect\nlanguage.name = Boarisch\nlanguage.fallback = de\nfarewell = Servus\n"));

        var chain = catalog.Chain(catalog.Find("bar")!);

        Assert.Equal(["bar", "de", "en"], chain.Select(language => language.Code));
    }

    [Fact]
    public void LanguagesDocumentIsUpToDate()
    {
        var expected = LanguagesDocument.Generate(LoadLanguages());
        var path = Path.Combine(TestPaths.RepositoryRoot, "LANGUAGES.md");

        if (Environment.GetEnvironmentVariable("UPDATE_LANGUAGES") == "1")
        {
            File.WriteAllText(path, expected);
        }

        Assert.True(File.Exists(path), "LANGUAGES.md is missing. Run the tests with UPDATE_LANGUAGES=1.");
        Assert.True(
            File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) == expected,
            "LANGUAGES.md is outdated. Run the tests with UPDATE_LANGUAGES=1.");
    }

    private static HashSet<string> Placeholders(string text) =>
        [.. PlaceholderPattern().Matches(text).Select(match => match.Groups[1].Value)];

    [GeneratedRegex(@"\{(\d+)(?::[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
