using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using Synopio.Engine.Localization;

namespace Synopio.Localization;

public sealed class Localizer : INotifyPropertyChanged
{
    // Constants
    private const string EmbeddedEnglishResource = "Synopio.lang.en.lang";

    // Fields
    private readonly LanguageCatalog _catalog;
    private IReadOnlyList<LanguageFile> _chain = [];
    private LanguageFile _current;

    // Constructor
    private Localizer()
    {
        _catalog = LanguageCatalog.Load(LoadEmbeddedEnglish(), LanguageDirectories);
        _current = _catalog.Resolve(null, PreferredUiLanguages());
        _chain = _catalog.Chain(_current);
    }

    // Properties
    public static IReadOnlyList<string> LanguageDirectories { get; } =
    [
        Path.Combine(AppContext.BaseDirectory, "lang"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Synopio", "lang"),
    ];

    public static Localizer Instance { get; } = new();

    public IReadOnlyList<LanguageFile> Languages => _catalog.Languages;

    public LanguageFile CurrentLanguage => _current;

    public FlowDirection FlowDirection => _current.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string this[string key]
    {
        get
        {
            foreach (var language in _chain)
            {
                if (language.Entries.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            return key;
        }
    }

    // Events
    public event PropertyChangedEventHandler? PropertyChanged;

    // Methods
    public string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, this[key], arguments);

    public void SetLanguage(string? code)
    {
        _current = _catalog.Resolve(code, PreferredUiLanguages());
        _chain = _catalog.Chain(_current);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
    }

    private static List<string> PreferredUiLanguages()
    {
        var languages = new List<string>();
        for (var culture = CultureInfo.CurrentUICulture; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            languages.Add(culture.Name);
        }

        return languages;
    }

    private static string LoadEmbeddedEnglish()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedEnglishResource)
            ?? throw new InvalidOperationException(EmbeddedEnglishResource);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
