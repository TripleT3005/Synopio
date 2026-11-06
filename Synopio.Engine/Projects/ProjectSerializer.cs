using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synopio.Engine.Projects;

public static class ProjectSerializer
{
    // Fields
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Methods
    public static void Save(string path, SynopioProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        var portable = project with
        {
            Left = ToRelative(project.Left, directory),
            Right = ToRelative(project.Right, directory),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(portable, Options));
    }

    public static SynopioProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<SynopioProject>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException(path);

        if (project.FormatVersion > SynopioProject.CurrentFormatVersion)
        {
            throw new NotSupportedException(project.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        return project with
        {
            Left = ToAbsolute(project.Left, directory),
            Right = ToAbsolute(project.Right, directory),
        };
    }

    private static ProjectDocument? ToRelative(ProjectDocument? document, string directory) =>
        document == null ? null : document with { Path = Path.GetRelativePath(directory, document.Path) };

    private static ProjectDocument? ToAbsolute(ProjectDocument? document, string directory) =>
        document == null ? null : document with { Path = Path.GetFullPath(Path.Combine(directory, document.Path)) };
}
