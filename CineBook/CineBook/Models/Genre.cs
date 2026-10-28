using System;
using System.Collections.Generic;

namespace CineBook.Models;

public partial class Genre
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
}
