using System.Windows;
using Synopio.Services;
using Synopio.ViewModels;

namespace Synopio.Views;

public partial class AboutWindow : Window
{
    // Constructor
    public AboutWindow(AboutViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    // Window
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowBackdrop.Disable(this);
    }
}
