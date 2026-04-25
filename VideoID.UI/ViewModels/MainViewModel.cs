using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using VideoID.Core.Models;
using VideoID.Core.Processing;
using VideoID.Core.Services;
using VideoID.Core.Storage;

namespace VideoID.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ProcessingOptions _options;
    private readonly VideoProcessingPipeline _pipeline;
    private readonly VideoScanService _scanService;
    private readonly FaceDatabase _db;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _faceFilter = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    private bool _isProcessing;
    [ObservableProperty] private int _totalFramesProcessed;
    [ObservableProperty] private int _totalFacesFound;
    [ObservableProperty] private int _uniqueFacesCount;
    [ObservableProperty] private bool _gpuAvailable;
    [ObservableProperty] private string _selectedTab = "Faces";

    public bool IsFacesTab    => SelectedTab == "Faces";
    public bool IsVideosTab   => SelectedTab == "Videos";
    public bool IsSettingsTab => SelectedTab == "Settings";
    public bool HasNoIdentities => Identities.Count == 0;

    public IEnumerable<FaceIdentityViewModel> FilteredIdentities =>
        string.IsNullOrWhiteSpace(FaceFilter)
            ? Identities
            : Identities.Where(i =>
                i.Name.Contains(FaceFilter, StringComparison.OrdinalIgnoreCase));

    public ObservableCollection<FaceIdentityViewModel> Identities { get; } = [];
    public ObservableCollection<VideoFileViewModel> Videos { get; } = [];
    public SettingsViewModel Settings { get; }

    private readonly Dictionary<Guid, FaceIdentityViewModel> _identityMap = [];
    private CancellationTokenSource? _cts;

    public MainViewModel(
        ProcessingOptions options,
        VideoProcessingPipeline pipeline,
        VideoScanService scanService,
        FaceDatabase db,
        SettingsViewModel settings)
    {
        _options  = options;
        _pipeline = pipeline;
        _scanService = scanService;
        _db       = db;
        Settings  = settings;

        // Wire pipeline events
        _pipeline.Progress       += OnProgress;
        _pipeline.FaceDiscovered += OnFaceDiscovered;
        _pipeline.VideoCompleted += OnVideoCompleted;
        _pipeline.PipelineError  += OnPipelineError;

        // Check CUDA availability
        GpuAvailable = CheckCuda();

        // Load persisted identities
        LoadPersistedIdentities();
    }

    // ─── Commands ────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScan()
    {
        IsProcessing = true;
        StatusText   = "Scanning video directories…";
        Videos.Clear();

        var videoFiles = _scanService.Scan();
        if (videoFiles.Count == 0)
        {
            StatusText   = "No video files found. Add directories in Settings.";
            IsProcessing = false;
            return;
        }

        foreach (var v in videoFiles)
            Videos.Add(new VideoFileViewModel(v));

        StatusText = $"Found {videoFiles.Count} video file(s). Starting GPU pipeline…";

        _cts = new CancellationTokenSource();
        try
        {
            await _pipeline.StartAsync(videoFiles, _cts.Token);
        }
        catch (Exception ex)
        {
            StatusText   = $"Pipeline error: {ex.Message}";
            IsProcessing = false;
        }
    }

    private bool CanStartScan() => !IsProcessing;
    private bool CanStopScan()  => IsProcessing;

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private async Task StopScan()
    {
        StatusText = "Stopping…";
        _cts?.Cancel();
        await _pipeline.StopAsync();
        IsProcessing = false;
        StatusText   = "Stopped.";
    }

    [RelayCommand]
    private void ClearIdentities()
    {
        var result = MessageBox.Show(
            "Clear all face identities? This cannot be undone.",
            "Confirm Clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        foreach (var id in _identityMap.Keys.ToList())
            _db.DeleteIdentity(id);

        _identityMap.Clear();
        Identities.Clear();
        UniquesFacesCount();
    }

    [RelayCommand]
    private void AddVideoDirectory()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a directory containing video files"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
            !_options.VideoDirectories.Contains(dialog.SelectedPath))
        {
            _options.VideoDirectories.Add(dialog.SelectedPath);
            StatusText = $"Added: {dialog.SelectedPath}";
        }
    }

    [RelayCommand]
    private void SetTab(string tab)
    {
        SelectedTab = tab;
        OnPropertyChanged(nameof(IsFacesTab));
        OnPropertyChanged(nameof(IsVideosTab));
        OnPropertyChanged(nameof(IsSettingsTab));
    }

    partial void OnFaceFilterChanged(string value) =>
        OnPropertyChanged(nameof(FilteredIdentities));

    [RelayCommand]
    private void ExportNames()
    {
        var lines = Identities
            .Where(i => i.IsNamed)
            .Select(i => $"{i.Name}\t{i.FaceCount} detections\t{i.VideoCount} video(s)");

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"VideoID_export_{DateTime.Now:yyyyMMdd_HHmmss}.tsv");

        File.WriteAllLines(path, lines);
        StatusText = $"Exported to {path}";
    }

    // ─── Pipeline event handlers (called from background threads) ────────────

    private void OnProgress(object? s, PipelineProgressEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TotalFramesProcessed++;

            var vm = Videos.FirstOrDefault(v => v.FilePath == e.Video.FilePath);
            vm?.SyncFromModel();
        });
    }

    private void OnFaceDiscovered(object? s, FaceDiscoveredEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TotalFacesFound++;

            if (_identityMap.TryGetValue(e.Identity.Id, out var existing))
            {
                existing.Refresh();
            }
            else
            {
                var vm = new FaceIdentityViewModel(e.Identity, _db);
                _identityMap[e.Identity.Id] = vm;
                Identities.Add(vm);
            }

            UniquesFacesCount();
            StatusText = $"{Identities.Count} unique face(s) found across {Videos.Count} video(s)";
        });
    }

    private void OnVideoCompleted(object? s, VideoFile video)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var vm = Videos.FirstOrDefault(v => v.FilePath == video.FilePath);
            vm?.SyncFromModel();

            bool allDone = Videos.All(v =>
                v.State is VideoProcessingState.Completed or VideoProcessingState.Failed);

            if (allDone)
            {
                IsProcessing = false;
                StatusText   = $"Complete. {Identities.Count} unique face(s) in {Videos.Count} video(s).";
            }
        });
    }

    private void OnPipelineError(object? s, Exception ex)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText   = $"Error: {ex.Message}";
            IsProcessing = false;
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void UniquesFacesCount()
    {
        UniqueFacesCount = Identities.Count;
        OnPropertyChanged(nameof(HasNoIdentities));
        OnPropertyChanged(nameof(FilteredIdentities));
    }

    private void LoadPersistedIdentities()
    {
        foreach (var identity in _db.LoadAllIdentities())
        {
            var vm = new FaceIdentityViewModel(identity, _db);
            _identityMap[identity.Id] = vm;
            Identities.Add(vm);
        }
        UniquesFacesCount();
    }

    private static bool CheckCuda()
    {
        try { return Emgu.CV.Cuda.CudaInvoke.HasCuda; }
        catch { return false; }
    }
}
