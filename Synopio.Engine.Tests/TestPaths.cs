namespace Synopio.Engine.Tests;

internal static class TestPaths
{
    // Properties
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Synopio.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Synopio.slnx");
        }
    }

    public static string LanguageDirectory => Path.Combine(RepositoryRoot, "lang");

    public static string TestDataDirectory => Path.Combine(RepositoryRoot, "testdata");
}
