using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CineBook.Models;

public sealed partial class CoverImageResult(Bitmap thumbnail, string imageUrl, string displayName) : ObservableObject
{
    public Bitmap Thumbnail { get; } = thumbnail;
    public string ImageUrl { get; } = imageUrl;
    public string DisplayName { get; } = displayName;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public void Dispose() => Dispatcher.UIThread.Post(Thumbnail.Dispose, DispatcherPriority.Background);
}
