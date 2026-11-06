namespace Synopio.Services;

public interface IDialogService
{
    // Methods
    string? OpenFile(string filter);

    string? SaveFile(string filter, string defaultFileName);

    bool Confirm(string message);

    void ShowError(string message);

    void ShowAbout();
}
