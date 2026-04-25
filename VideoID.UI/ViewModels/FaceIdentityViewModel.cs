using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoID.Core.Models;
using VideoID.Core.Storage;

namespace VideoID.UI.ViewModels;

public sealed partial class FaceIdentityViewModel : ObservableObject
{
    private readonly FaceIdentity _model;
    private readonly FaceDatabase _db;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private ImageSource? _thumbnail;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = "";

    public Guid Id => _model.Id;
    public int FaceCount => _model.FaceCount;
    public int VideoCount => _model.SourceVideos.Count;
    public bool HasThumbnail => Thumbnail != null;
    public IReadOnlyCollection<string> SourceVideos => _model.SourceVideos;
    public bool IsNamed => _model.IsNamed;

    public FaceIdentityViewModel(FaceIdentity model, FaceDatabase db)
    {
        _model = model;
        _db    = db;
        _name  = model.Name ?? "(unnamed)";
        LoadThumbnail(model.Thumbnail);
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditName  = _model.Name ?? "";
        IsEditing = true;
    }

    [RelayCommand]
    private void CommitName()
    {
        var trimmed = EditName.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            _model.Name = trimmed;
            _db.UpsertIdentity(_model);
            Name = trimmed;
        }
        IsEditing = false;
        OnPropertyChanged(nameof(IsNamed));
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    /// <summary>Update face count and thumbnail after new detections are merged in.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(FaceCount));
        OnPropertyChanged(nameof(VideoCount));
        LoadThumbnail(_model.Thumbnail);
    }

    private void LoadThumbnail(byte[] jpegBytes)
    {
        if (jpegBytes is not { Length: > 0 }) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(jpegBytes);
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            Thumbnail = bmp;
        }
        catch { /* silently skip corrupt thumbnail */ }
    }
}
