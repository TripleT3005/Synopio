using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Synopio.Services;
using Synopio.ViewModels;

namespace Synopio.Views;

public partial class MainWindow : Window
{
    // Fields
    private readonly MainViewModel _viewModel;
    private readonly WindowPlacementService _placementService;

    // Constructor
    public MainWindow(MainViewModel viewModel, WindowPlacementService placementService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _placementService = placementService ?? throw new ArgumentNullException(nameof(placementService));
        DataContext = viewModel;
        InitializeComponent();
    }

    // Window placement
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowBackdrop.Disable(this);
        _placementService.Restore(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _placementService.Save(this);
        base.OnClosing(e);
    }

    // Drag and drop
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnLeftDrop(object sender, DragEventArgs e) => await DropAsync(e, _viewModel.Left);

    private async void OnRightDrop(object sender, DragEventArgs e) => await DropAsync(e, _viewModel.Right);

    private async Task DropAsync(DragEventArgs e, DocumentSlotViewModel slot)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await _viewModel.LoadDocumentAsync(slot, files[0]);
        }
    }

    // Synopsis list
    private void OnSynopsisSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SynopsisList.SelectedItem != null)
        {
            SynopsisList.ScrollIntoView(SynopsisList.SelectedItem);
        }
    }

    private void OnCopyRows(object sender, ExecutedRoutedEventArgs e)
    {
        var text = new StringBuilder();
        foreach (var row in SynopsisList.SelectedItems.OfType<SynopsisRowViewModel>().OrderBy(row => row.Number))
        {
            text.AppendLine(row.PlainText);
        }

        if (text.Length > 0)
        {
            Clipboard.SetText(text.ToString());
        }
    }
}
