using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CineBook.Models;

public partial class Genre : ObservableObject
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();

    [NotMapped] [ObservableProperty] public partial bool IsSelected { get; set; } = false;
}