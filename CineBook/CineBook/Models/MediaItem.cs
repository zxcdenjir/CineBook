using System;
using System.Collections.Generic;
using System.Linq;

namespace CineBook.Models;

public partial class MediaItem
{
    public int Id { get; set; }

    public int TypeId { get; set; }

    public string Title { get; set; } = null!;

    public string? Creator { get; set; }

    public int? Duration { get; set; }

    public double? Rating { get; set; }

    public string? Description { get; set; }

    public int StatusId { get; set; }

    public string? PosterPath { get; set; }

    public bool IsFavorite { get; set; }

    public virtual Status Status { get; set; } = null!;

    public virtual Type Type { get; set; } = null!;

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();
    
    public double? Stars => Rating / 2;
    
    public string GenresString => Genres.Count > 0 ? string.Join(" • ", Genres.Select(g => g.Name).Take(2)) : "Нет жанра";
}
