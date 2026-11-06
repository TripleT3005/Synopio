using System.Windows;
using Microsoft.Win32;
using Synopio.Localization;
using Synopio.ViewModels;
using Synopio.Views;

namespace Synopio.Services;

public sealed class DialogService : IDialogService
{
    // Methods
    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName, AddExtension = true, OverwritePrompt = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, Localizer.Instance["app.title"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, Localizer.Instance["app.title"], MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowAbout() =>
        new AboutWindow(new AboutViewModel()) { Owner = Application.Current.MainWindow }.ShowDialog();
}
