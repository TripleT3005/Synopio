using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Synopio.Engine.Comparison;

namespace Synopio.Engine.Tests;

public partial class VersionTests
{
    // Helpers
    private static string ProjectVersion =>
        XDocument.Load(Path.Combine(TestPaths.RepositoryRoot, "Directory.Build.props")).Descendants("Version").Single().Value.Trim();

    private static string Changelog => File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "CHANGELOG.md")).Replace("\r\n", "\n", StringComparison.Ordinal);

    // Tests
    [Fact]
    public void VersionIsSemantic()
    {
        Assert.Matches(SemanticVersionPattern(), ProjectVersion);
    }

    [Fact]
    public void AssembliesUseProjectVersion()
    {
        var informational = typeof(SynopsisBuilder).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

        Assert.Equal(ProjectVersion, informational.Split('+')[0]);
    }

    [Fact]
    public void ChangelogStartsWithSectionOfCurrentVersion()
    {
        var sections = ReleaseHeadingPattern().Matches(Changelog).Select(match => match.Groups["version"].Value).ToList();

        Assert.NotEmpty(sections);
        Assert.Equal(ProjectVersion, sections[0]);
        Assert.Equal(sections.Count, sections.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ChangelogSectionOfCurrentVersionIsBilingual()
    {
        var heading = ReleaseHeadingPattern().Matches(Changelog).First(match => match.Groups["version"].Value == ProjectVersion);
        var next = Changelog.IndexOf("\n## [", heading.Index + heading.Length, StringComparison.Ordinal);
        var section = next < 0 ? Changelog[heading.Index..] : Changelog[heading.Index..next];

        Assert.Contains("\n### English\n", section, StringComparison.Ordinal);
        Assert.Contains("\n### Deutsch\n", section, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$")]
    private static partial Regex SemanticVersionPattern();

    [GeneratedRegex(@"^## \[(?<version>\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?)\] – \d{4}-\d{2}-\d{2}$", RegexOptions.Multiline)]
    private static partial Regex ReleaseHeadingPattern();
}
