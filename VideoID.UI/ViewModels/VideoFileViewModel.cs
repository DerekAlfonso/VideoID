using CommunityToolkit.Mvvm.ComponentModel;
using VideoID.Core.Models;

namespace VideoID.UI.ViewModels;

public sealed partial class VideoFileViewModel : ObservableObject
{
    private readonly VideoFile _model;

    [ObservableProperty] private VideoProcessingState _state;
    [ObservableProperty] private int _framesProcessed;
    [ObservableProperty] private int _facesFound;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string? _errorMessage;

    public string FileName    => _model.FileName;
    public string FilePath    => _model.FilePath;
    public TimeSpan Duration  => _model.Duration;
    public int TotalFrames    => _model.TotalFrames;
    public long FileSizeBytes => _model.FileSizeBytes;

    public string FileSizeText => FileSizeBytes switch
    {
        >= 1_073_741_824 => $"{FileSizeBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        _                => $"{FileSizeBytes / 1024.0:F1} KB"
    };

    public string StateText => State switch
    {
        VideoProcessingState.Pending   => "Waiting",
        VideoProcessingState.Scanning  => "Processing",
        VideoProcessingState.Completed => "Done",
        VideoProcessingState.Failed    => "Failed",
        _                              => "Unknown"
    };

    public VideoFileViewModel(VideoFile model)
    {
        _model          = model;
        _state          = model.State;
        _framesProcessed = model.FramesProcessed;
        _facesFound     = model.FacesFound;
        _progressPercent = model.ProgressPercent;
        _errorMessage   = model.ErrorMessage;
    }

    public void SyncFromModel()
    {
        State           = _model.State;
        FramesProcessed = _model.FramesProcessed;
        FacesFound      = _model.FacesFound;
        ProgressPercent = _model.ProgressPercent;
        ErrorMessage    = _model.ErrorMessage;
        OnPropertyChanged(nameof(TotalFrames));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(StateText));
    }
}
