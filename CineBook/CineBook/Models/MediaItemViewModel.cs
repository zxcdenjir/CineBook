using CineBook.ViewModels;

namespace CineBook.Models;

public class MediaItemViewModel(MediaItem item) : ViewModelBase
{
    public MediaItem MediaItem { get; set; } = item;

    public bool HasDescription => !string.IsNullOrWhiteSpace(MediaItem.Description);
}
