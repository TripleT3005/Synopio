using System.Windows;
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
}
