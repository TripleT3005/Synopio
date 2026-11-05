using System.IO;
using System.Windows;
using Synopio.Engine.Projects;
using Synopio.Services;
using Synopio.ViewModels;
using Synopio.Views;

namespace Synopio;

public partial class App : Application
{
    // Startup
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainViewModel(new DialogService(), new SettingsService());
        var window = new MainWindow(viewModel, new WindowPlacementService());
        MainWindow = window;
        window.Show();

        var files = e.Args.Where(File.Exists).ToList();
        var project = files.FirstOrDefault(file => string.Equals(Path.GetExtension(file), SynopioProject.FileExtension, StringComparison.OrdinalIgnoreCase));
        if (project != null)
        {
            await viewModel.LoadProjectAsync(project);
            return;
        }

        if (files.Count > 0)
        {
            await viewModel.LoadDocumentAsync(viewModel.Left, files[0]);
        }

        if (files.Count > 1)
        {
            await viewModel.LoadDocumentAsync(viewModel.Right, files[1]);
        }
    }
}
