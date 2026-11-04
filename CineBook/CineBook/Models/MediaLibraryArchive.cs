using System.Collections.Generic;

namespace CineBook.Models;

public sealed class MediaLibraryArchive
{
    public int Version { get; set; } = 1;
    public List<MediaLibraryArchiveItem> Items { get; set; } = [];
}

public sealed class MediaLibraryArchiveItem
{
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public int? Duration { get; set; }
    public double? Rating { get; set; }
    public string? Description { get; set; }
    public bool IsFavorite { get; set; }
    public string AdditionDateTime { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = [];
    public string? PosterExtension { get; set; }
    public string? PosterBase64 { get; set; }
}
