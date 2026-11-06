using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;
using Synopio.Core;
using Synopio.Engine.Comparison;
using Synopio.Engine.Documents;
using Synopio.Engine.Export;
using Synopio.Engine.Projects;
using Synopio.Localization;
using Synopio.Services;

namespace Synopio.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    // Fields
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private Synopsis? _synopsis;
    private SynopsisRowViewModel? _selectedRow;
    private string _status;
    private bool _onlyChanges;
    private bool _ignoreCase;
    private bool _ignoreHyphens = true;
    private bool _ignorePunctuation;
    private bool _isComparing;
    private bool _comparePending;
    private readonly DispatcherTimer _compareTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private int _selectedTabIndex;
    private IReadOnlyList<LanguageOption> _languages;

    // Constructor
    public MainViewModel(IDialogService dialogs, SettingsService settingsService)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settings = _settingsService.Load();
        Localizer.Instance.SetLanguage(_settings.Language);
        _languages = CreateLanguageOptions();
        _status = Localizer.Instance["status.ready"];

        Left = new DocumentSlotViewModel("document.left");
        Right = new DocumentSlotViewModel("document.right");
        Left.Partner = Right;
        Right.Partner = Left;

        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = item => !OnlyChanges || item is SynopsisRowViewModel { HasChanges: true };

        OpenLeftCommand = new AsyncRelayCommand(() => OpenDocumentAsync(Left), () => !Left.IsBusy);
        OpenRightCommand = new AsyncRelayCommand(() => OpenDocumentAsync(Right), () => !Right.IsBusy);
        RegenerateLeftCommand = new AsyncRelayCommand(() => RegenerateAsync(Left), () => Left.HasDocument && !Left.IsBusy);
        RegenerateRightCommand = new AsyncRelayCommand(() => RegenerateAsync(Right), () => Right.HasDocument && !Right.IsBusy);
        ExportDocxCommand = new RelayCommand(_ => ExportDocx(), _ => _synopsis != null);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => Left.HasDocument || Right.HasDocument);
        NextChangeCommand = new RelayCommand(_ => MoveToChange(1), _ => _synopsis != null);
        PreviousChangeCommand = new RelayCommand(_ => MoveToChange(-1), _ => _synopsis != null);
        AboutCommand = new RelayCommand(_ => _dialogs.ShowAbout());

        Localizer.Instance.PropertyChanged += (_, _) => RefreshLocalizedProperties();

        _compareTimer.Tick += async (_, _) =>
        {
            _compareTimer.Stop();
            await CompareAsync();
        };
        Left.PropertyChanged += OnDocumentChanged;
        Right.PropertyChanged += OnDocumentChanged;
    }

    // Properties
    public DocumentSlotViewModel Left { get; }

    public DocumentSlotViewModel Right { get; }

    public ObservableCollection<SynopsisRowViewModel> Rows { get; } = [];

    public ICollectionView RowsView { get; }

    public IReadOnlyList<LanguageOption> Languages => _languages;

    public LanguageOption? SelectedLanguage
    {
        get => Languages.FirstOrDefault(option => option.Code == _settings.Language) ?? Languages[0];
        set
        {
            if (value == null || value.Code == _settings.Language)
            {
                return;
            }

            _settings = _settings with { Language = value.Code };
            _settingsService.Save(_settings);
            Localizer.Instance.SetLanguage(value.Code);
        }
    }

    public SynopsisRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Statistics => _synopsis?.Statistics is { } statistics
        ? Localizer.Instance.Format("status.statistics", statistics.ModifiedRows, statistics.DeletedRows, statistics.InsertedRows, statistics.DeletedWords, statistics.InsertedWords)
        : string.Empty;

    public bool IsComparing
    {
        get => _isComparing;
        private set => SetProperty(ref _isComparing, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool OnlyChanges
    {
        get => _onlyChanges;
        set
        {
            if (SetProperty(ref _onlyChanges, value))
            {
                RowsView.Refresh();
            }
        }
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetAndCompare(ref _ignoreCase, value);
    }

    public bool IgnoreHyphens
    {
        get => _ignoreHyphens;
        set => SetAndCompare(ref _ignoreHyphens, value);
    }

    public bool IgnorePunctuation
    {
        get => _ignorePunctuation;
        set => SetAndCompare(ref _ignorePunctuation, value);
    }

    public AsyncRelayCommand OpenLeftCommand { get; }

    public AsyncRelayCommand OpenRightCommand { get; }

    public AsyncRelayCommand RegenerateLeftCommand { get; }

    public AsyncRelayCommand RegenerateRightCommand { get; }



    public RelayCommand ExportDocxCommand { get; }

    public AsyncRelayCommand OpenProjectCommand { get; }

    public RelayCommand SaveProjectCommand { get; }

    public RelayCommand NextChangeCommand { get; }

    public RelayCommand PreviousChangeCommand { get; }

    public RelayCommand AboutCommand { get; }

    private ComparisonOptions ComparisonOptions => new()
    {
        IgnoreCase = IgnoreCase,
        IgnoreHyphens = IgnoreHyphens,
        IgnorePunctuation = IgnorePunctuation,
    };

    // Methods
    public async Task LoadDocumentAsync(DocumentSlotViewModel slot, string path)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (!DocumentLoader.SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            _dialogs.ShowError(Localizer.Instance.Format("error.unsupportedFile", Path.GetFileName(path)));
            return;
        }

        if (slot.IsEdited && !_dialogs.Confirm(Localizer.Instance["confirm.discardEdits"]))
        {
            return;
        }

        Status = Localizer.Instance.Format("status.loading", Path.GetFileName(path));
        try
        {
            await slot.LoadAsync(path);
            if (slot.Partner is { Source: not null, IsEdited: false } partner)
            {
                await partner.RegenerateTextAsync();
            }

            Status = Localizer.Instance.Format("status.loaded", slot.FileName, slot.Source?.PageCount ?? 0);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or UglyToad.PdfPig.Core.PdfDocumentFormatException)
        {
            Status = Localizer.Instance["status.ready"];
            _dialogs.ShowError(Localizer.Instance.Format("error.loadFailed", Path.GetFileName(path), exception.Message));
        }
    }

    private async Task OpenDocumentAsync(DocumentSlotViewModel slot)
    {
        var extensions = string.Join(";", DocumentLoader.SupportedExtensions.Select(extension => "*" + extension));
        var path = _dialogs.OpenFile($"{Localizer.Instance["filter.documents"]}|{extensions}|{Localizer.Instance["filter.all"]}|*.*");
        if (path != null)
        {
            await LoadDocumentAsync(slot, path);
        }
    }

    private async Task RegenerateAsync(DocumentSlotViewModel slot)
    {
        if (slot.IsEdited && !_dialogs.Confirm(Localizer.Instance["confirm.discardEdits"]))
        {
            return;
        }

        await slot.RegenerateTextAsync();
    }

    private bool CanCompare() => Left.HasDocument && Right.HasDocument && !Left.IsBusy && !Right.IsBusy;

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentSlotViewModel.Text) or nameof(DocumentSlotViewModel.IsBusy))
        {
            ScheduleCompare();
        }
    }

    private void SetAndCompare(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            ScheduleCompare();
        }
    }

    private void ScheduleCompare()
    {
        _compareTimer.Stop();
        if (CanCompare())
        {
            _compareTimer.Start();
        }
    }

    private async Task CompareAsync()
    {
        if (!CanCompare())
        {
            return;
        }

        if (IsComparing)
        {
            _comparePending = true;
            return;
        }

        IsComparing = true;
        Status = Localizer.Instance["status.comparing"];
        try
        {
            var left = Left.GetParagraphs();
            var right = Right.GetParagraphs();
            var options = ComparisonOptions;
            var stopwatch = Stopwatch.StartNew();
            var synopsis = await Task.Run(() => SynopsisBuilder.Compare(left, right, options));

            _synopsis = synopsis;
            Rows.Clear();
            var number = 1;
            foreach (var row in synopsis.Rows)
            {
                Rows.Add(new SynopsisRowViewModel(row, number++));
            }

            SelectedRow = null;
            SelectedTabIndex = 0;
            OnPropertyChanged(nameof(Statistics));
            Status = Localizer.Instance.Format("status.compared", stopwatch.Elapsed.TotalSeconds);
        }
        finally
        {
            IsComparing = false;
            if (_comparePending)
            {
                _comparePending = false;
                ScheduleCompare();
            }
        }
    }

    private void MoveToChange(int direction)
    {
        var visible = RowsView.Cast<SynopsisRowViewModel>().ToList();
        if (visible.Count == 0)
        {
            return;
        }

        var start = SelectedRow == null ? (direction > 0 ? -1 : visible.Count) : visible.IndexOf(SelectedRow);
        for (var index = start + direction; index >= 0 && index < visible.Count; index += direction)
        {
            if (visible[index].HasChanges)
            {
                SelectedRow = visible[index];
                return;
            }
        }

        Status = Localizer.Instance[direction > 0 ? "status.noNextChange" : "status.noPreviousChange"];
    }

    private void ExportDocx()
    {
        if (_synopsis == null)
        {
            return;
        }

        var defaultName = $"{Localizer.Instance["export.defaultFileName"]} {Path.GetFileNameWithoutExtension(Left.FileName)} - {Path.GetFileNameWithoutExtension(Right.FileName)}.docx";
        var path = _dialogs.SaveFile($"{Localizer.Instance["filter.word"]}|*.docx", SanitizeFileName(defaultName));
        if (path == null)
        {
            return;
        }

        try
        {
            using (var stream = File.Create(path))
            {
                DocxSynopsisWriter.Write(stream, _synopsis, new DocxExportOptions
                {
                    Title = Localizer.Instance["export.title"],
                    LeftHeading = Left.FileName,
                    RightHeading = Right.FileName,
                    PageLabel = Localizer.Instance["export.pageLabel"],
                    OnlyChangedRows = OnlyChanges,
                    FooterText = $"{Localizer.Instance.Format("export.createdWith", AboutViewModel.Version)} · {AboutViewModel.WebsiteDisplay}",
                });
            }

            Status = Localizer.Instance.Format("status.exported", Path.GetFileName(path));
            if (_dialogs.Confirm(Localizer.Instance["confirm.openExport"]))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Localizer.Instance.Format("error.saveFailed", exception.Message));
        }
    }

    private void SaveProject()
    {
        var path = _dialogs.SaveFile($"{Localizer.Instance["filter.project"]}|*{SynopioProject.FileExtension}", Localizer.Instance["project.defaultFileName"] + SynopioProject.FileExtension);
        if (path == null)
        {
            return;
        }

        try
        {
            ProjectSerializer.Save(path, new SynopioProject
            {
                Left = Left.ToProjectDocument(),
                Right = Right.ToProjectDocument(),
                Comparison = ComparisonOptions,
            });
            Status = Localizer.Instance.Format("status.projectSaved", Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Localizer.Instance.Format("error.saveFailed", exception.Message));
        }
    }

    private async Task OpenProjectAsync()
    {
        var path = _dialogs.OpenFile($"{Localizer.Instance["filter.project"]}|*{SynopioProject.FileExtension}");
        if (path != null)
        {
            await LoadProjectAsync(path);
        }
    }

    public async Task LoadProjectAsync(string path)
    {
        try
        {
            var project = ProjectSerializer.Load(path);
            IgnoreCase = project.Comparison.IgnoreCase;
            IgnoreHyphens = project.Comparison.IgnoreHyphens;
            IgnorePunctuation = project.Comparison.IgnorePunctuation;

            foreach (var (slot, document) in new[] { (Left, project.Left), (Right, project.Right) })
            {
                if (document == null)
                {
                    continue;
                }

                if (!File.Exists(document.Path))
                {
                    _dialogs.ShowError(Localizer.Instance.Format("error.projectFileMissing", document.Path));
                    continue;
                }

                if (DocumentLoader.ComputeSha256(document.Path) != document.Sha256 && !_dialogs.Confirm(Localizer.Instance.Format("confirm.fileChanged", Path.GetFileName(document.Path))))
                {
                    continue;
                }

                await slot.ApplyProjectAsync(document);
            }

            Status = Localizer.Instance.Format("status.projectLoaded", Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _dialogs.ShowError(Localizer.Instance.Format("error.loadFailed", Path.GetFileName(path), exception.Message));
        }
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Statistics));
        _languages = CreateLanguageOptions();
        OnPropertyChanged(nameof(Languages));
        OnPropertyChanged(nameof(SelectedLanguage));
        Left.RefreshLocalizedProperties();
        Right.RefreshLocalizedProperties();
        RowsView.Refresh();
    }

    private static List<LanguageOption> CreateLanguageOptions() =>
        [
            new LanguageOption(null, Localizer.Instance["settings.language.automatic"]),
            .. Localizer.Instance.Languages.Select(language => new LanguageOption(language.Code, language.Name)),
        ];

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}
