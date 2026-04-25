using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Forms;
using VideoID.Core.Models;

namespace VideoID.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ProcessingOptions _options;

    [ObservableProperty] private int _frameSkip;
    [ObservableProperty] private int _maxParallelVideos;
    [ObservableProperty] private int _gpuWorkers;
    [ObservableProperty] private float _faceMatchThreshold;
    [ObservableProperty] private float _detectionConfidence;
    [ObservableProperty] private bool _useGpu;
    [ObservableProperty] private bool _writeTagsToFiles;
    [ObservableProperty] private string _modelDirectory = "";
    [ObservableProperty] private string _databasePath = "";
    [ObservableProperty] private float _detectionScale;
    [ObservableProperty] private int _thumbnailSize;
    [ObservableProperty] private string _videoDirectoriesText = "";

    public SettingsViewModel(ProcessingOptions options)
    {
        _options = options;
        Load();
    }

    private void Load()
    {
        FrameSkip              = _options.FrameSkip;
        MaxParallelVideos      = _options.MaxParallelVideos;
        GpuWorkers             = _options.GpuWorkers;
        FaceMatchThreshold     = _options.FaceMatchThreshold;
        DetectionConfidence    = _options.DetectionConfidence;
        UseGpu                 = _options.UseGpu;
        WriteTagsToFiles       = _options.WriteTagsToFiles;
        ModelDirectory         = _options.ModelDirectory;
        DatabasePath           = _options.DatabasePath;
        DetectionScale         = _options.DetectionScale;
        ThumbnailSize          = _options.ThumbnailSize;
        VideoDirectoriesText   = string.Join("\n", _options.VideoDirectories);
    }

    [RelayCommand]
    public void Apply()
    {
        _options.FrameSkip           = Math.Max(1, FrameSkip);
        _options.MaxParallelVideos   = Math.Max(1, MaxParallelVideos);
        _options.GpuWorkers          = Math.Max(1, GpuWorkers);
        _options.FaceMatchThreshold  = Math.Clamp(FaceMatchThreshold, 0.01f, 1.0f);
        _options.DetectionConfidence = Math.Clamp(DetectionConfidence, 0.01f, 1.0f);
        _options.UseGpu              = UseGpu;
        _options.WriteTagsToFiles    = WriteTagsToFiles;
        _options.ModelDirectory      = ModelDirectory;
        _options.DatabasePath        = DatabasePath;
        _options.DetectionScale      = Math.Clamp(DetectionScale, 0.1f, 1.0f);
        _options.ThumbnailSize       = Math.Clamp(ThumbnailSize, 32, 512);
        _options.VideoDirectories    = VideoDirectoriesText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    [RelayCommand]
    private void BrowseModelDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select model directory" };
        if (dialog.ShowDialog() == DialogResult.OK)
            ModelDirectory = dialog.SelectedPath;
    }

    [RelayCommand]
    private void AddVideoDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Add video directory" };
        if (dialog.ShowDialog() == DialogResult.OK)
            VideoDirectoriesText = string.IsNullOrEmpty(VideoDirectoriesText)
                ? dialog.SelectedPath
                : VideoDirectoriesText + "\n" + dialog.SelectedPath;
    }
}
