using System.Diagnostics;
using System.Reflection;
using Synopio.Core;

namespace Synopio.ViewModels;

public sealed class AboutViewModel
{
    // Constants
    public const string WebsiteUrl = "https://ckass.de/synopio/";
    public const string WebsiteDisplay = "ckass.de/synopio";
    public const string IssuesUrl = "https://github.com/TripleT3005/Synopio/issues";

    // Constructor
    public AboutViewModel()
    {
        OpenLinkCommand = new RelayCommand(parameter =>
        {
            if (parameter is string url && url.StartsWith("https://", StringComparison.Ordinal))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        });
    }

    // Properties
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var separator = version.IndexOf('+', StringComparison.Ordinal);
            return separator < 0 ? version : version[..separator];
        }
    }

    public static string Copyright => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    public RelayCommand OpenLinkCommand { get; }
}
