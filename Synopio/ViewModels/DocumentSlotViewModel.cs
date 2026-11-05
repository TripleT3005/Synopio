using System.IO;
using Synopio.Core;
using Synopio.Engine.Documents;
using Synopio.Engine.Projects;
using Synopio.Engine.Text;
using Synopio.Localization;

namespace Synopio.ViewModels;

public sealed class DocumentSlotViewModel : ObservableObject
{
    // Fields
    private string? _filePath;
    private SourceDocument? _source;
    private string _text = string.Empty;
    private bool _isEdited;
    private bool _isBusy;
    private bool _isApplyingText;
    private int? _firstPage;
    private int? _lastPage;
    private bool _removeHeadersAndFooters = true;
    private bool _repairLigatures = true;
    private int _preparationVersion;

    // Constructor
    public DocumentSlotViewModel(string headingKey)
    {
        HeadingKey = headingKey;
    }

    // Properties
    public string HeadingKey { get; }

    public string Heading => Localizer.Instance[HeadingKey];

    public DocumentSlotViewModel? Partner { get; set; }

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(HasDocument));
            }
        }
    }

    public string FileName => FilePath == null ? Localizer.Instance["document.none"] : Path.GetFileName(FilePath);

    public bool HasDocument => FilePath != null;

    public SourceDocument? Source
    {
        get => _source;
        private set
        {
            if (SetProperty(ref _source, value))
            {
                OnPropertyChanged(nameof(PageInfo));
            }
        }
    }

    public string PageInfo => Source == null ? string.Empty : Localizer.Instance.Format("document.pageCount", Source.PageCount);

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value) && !_isApplyingText)
            {
                IsEdited = true;
            }
        }
    }

    public bool IsEdited
    {
        get => _isEdited;
        private set => SetProperty(ref _isEdited, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public int? FirstPage
    {
        get => _firstPage;
        set
        {
            if (SetProperty(ref _firstPage, value))
            {
                PreparationSettingChanged();
            }
        }
    }

    public int? LastPage
    {
        get => _lastPage;
        set
        {
            if (SetProperty(ref _lastPage, value))
            {
                PreparationSettingChanged();
            }
        }
    }

    public bool RemoveHeadersAndFooters
    {
        get => _removeHeadersAndFooters;
        set
        {
            if (SetProperty(ref _removeHeadersAndFooters, value))
            {
                PreparationSettingChanged();
            }
        }
    }

    public bool RepairLigatures
    {
        get => _repairLigatures;
        set
        {
            if (SetProperty(ref _repairLigatures, value))
            {
                PreparationSettingChanged();
            }
        }
    }

    // Events
    public event EventHandler? TextRegenerated;

    // Methods
    public async Task LoadAsync(string path)
    {
        IsBusy = true;
        try
        {
            var source = await Task.Run(() => DocumentLoader.Load(path));
            FilePath = path;
            Source = source;
            _firstPage = null;
            _lastPage = null;
            OnPropertyChanged(nameof(FirstPage));
            OnPropertyChanged(nameof(LastPage));
            await RegenerateTextAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyProjectAsync(ProjectDocument document)
    {
        await LoadAsync(document.Path);
        _firstPage = document.FirstPage;
        _lastPage = document.LastPage;
        _removeHeadersAndFooters = document.RemoveHeadersAndFooters;
        _repairLigatures = document.RepairLigatures;
        OnPropertyChanged(nameof(FirstPage));
        OnPropertyChanged(nameof(LastPage));
        OnPropertyChanged(nameof(RemoveHeadersAndFooters));
        OnPropertyChanged(nameof(RepairLigatures));

        if (document.EditedText != null)
        {
            Text = document.EditedText;
        }
        else
        {
            await RegenerateTextAsync();
        }
    }

    public ProjectDocument? ToProjectDocument() =>
        FilePath == null || Source == null
            ? null
            : new ProjectDocument
            {
                Path = FilePath,
                Sha256 = Source.Sha256,
                FirstPage = FirstPage,
                LastPage = LastPage,
                RemoveHeadersAndFooters = RemoveHeadersAndFooters,
                RepairLigatures = RepairLigatures,
                EditedText = IsEdited ? Text : null,
            };

    public IReadOnlyList<Paragraph> GetParagraphs() => PreparedText.Parse(Text);

    public async Task RegenerateTextAsync()
    {
        if (Source is not { } source)
        {
            return;
        }

        var version = ++_preparationVersion;
        var options = new PreparationOptions
        {
            FirstPage = FirstPage,
            LastPage = LastPage,
            RemoveHeadersAndFooters = RemoveHeadersAndFooters,
            RepairLigatures = RepairLigatures,
        };
        var reference = Partner?.Source;

        IsBusy = true;
        try
        {
            var text = await Task.Run(() => PreparedText.Format(TextPreparer.Prepare(source, options, reference)));
            if (version != _preparationVersion)
            {
                return;
            }

            _isApplyingText = true;
            Text = text;
            _isApplyingText = false;
            IsEdited = false;
            TextRegenerated?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(PageInfo));
    }

    private async void PreparationSettingChanged()
    {
        if (!IsEdited)
        {
            await RegenerateTextAsync();
        }
    }
}
