using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CineBook.Models;

public partial class MediaItem : ObservableObject
{
    private static readonly (string Start, string End)[] PlaceholderPalette =
    [
        ("#5B7B9A", "#33506B"), // blue
        ("#C8A96E", "#96794B"), // ochre
        ("#6FA98C", "#4A7D66"), // sage
        ("#C6866A", "#9A5F49"), // terracotta
        ("#7E6B9E", "#564872"), // violet
        ("#3E4A63", "#232C40"), // midnight blue
        ("#8A9A5B", "#5F6D3A"), // olive
        ("#64748B", "#3F4A5C"), // slate
        ("#D29B8D", "#A96859"), // dusty rose
        ("#72A0A5", "#456E73"), // muted teal
        ("#B98F62", "#86613E"), // caramel
        ("#8A9EAD", "#586F80"), // mist blue
        ("#A77B86", "#754F5B"), // mauve
        ("#A3A56D", "#70713F"), // moss
        ("#7895B5", "#4D6685"), // cornflower
        ("#D0A878", "#9D754B"), // sand
        ("#719A83", "#49705D"), // eucalyptus
        ("#A87D62", "#77533F"), // umber
        ("#827DA6", "#5B557D"), // lavender slate
        ("#8E9A9C", "#5D696B") // stone
    ];

    public int Id { get; set; }

    public int TypeId { get; set; }

    public string Title { get; set; } = null!;

    public string? Creator { get; set; }

    public int? Duration { get; set; }

    public double? Rating { get; set; }

    public string? Description { get; set; }

    public int StatusId { get; set; }

    public string? PosterPath { get; set; }
    
    public string AdditionDateTime { get; set; }

    [ObservableProperty] public partial bool IsFavorite { get; set; }

    public virtual Status Status { get; set; } = null!;

    public virtual Type Type { get; set; } = null!;

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();

    [NotMapped] public double? Stars => Rating / 2;

    [NotMapped]
    public string GenresString =>
        Genres.Count > 0 ? string.Join(" • ", Genres.Select(g => g.Name).Take(2)) : "Нет жанра";

    [NotMapped]
    public string GenresStringFull => Genres.Count > 0 ? string.Join(", ", Genres.Select(g => g.Name)) : "Нет жанра";

    [NotMapped]
    public Geometry PosterPlaceholderIcon => Geometry.Parse(TypeId switch
    {
        1 =>
            "M6,4 C4.895,4 4,4.895 4,6 L4,18 C4,19.105 4.895,20 6,20 L18,20 C19.105,20 20,19.105 20,18 L20,6 C20,4.895 19.105,4 18,4 Z M8,4 L8,20 M16,4 L16,20 M4,8 L8,8 M4,12 L8,12 M4,16 L8,16 M16,8 L20,8 M16,12 L20,12 M16,16 L20,16",
        2 =>
            "M3.14067 4.7652H17.2197C18.4159 4.7652 19.3857 5.73495 19.3857 6.9312V15.5952C19.3857 16.7914 18.4159 17.7612 17.2197 17.7612H3.14067C1.94442 17.7612 0.97467 16.7914 0.97467 15.5952V6.9312C0.97467 5.73495 1.94442 4.7652 3.14067 4.7652Z M6.38965 0.97467L10.1801 4.76517L13.9706 0.97467",
        3 =>
            "M3.03247 0.97467H14.9455C15.3763 0.97467 15.7895 1.14582 16.0942 1.45048C16.3988 1.75513 16.57 2.16833 16.57 2.59917V17.7612C16.57 18.192 16.3988 18.6052 16.0942 18.9099C15.7895 19.2145 15.3763 19.3857 14.9455 19.3857H3.03247V0.97467Z M3.03243 0.97467C2.4867 0.97467 1.96331 1.19146 1.57742 1.57736C1.19152 1.96325 0.974731 2.48663 0.974731 3.03237V17.328C0.974731 17.8737 1.19152 18.3971 1.57742 18.783C1.76849 18.9741 1.99533 19.1256 2.24498 19.229C2.49464 19.3324 2.76221 19.3857 3.03243 19.3857 M6.28143 5.84814H12.7794M6.28143 9.63864H12.7794",
        _ => "M12,4 L20,12 L12,20 L4,12 Z"
    });

    [NotMapped]
    public IBrush PosterPlaceholderBrush
    {
        get
        {
            var paletteIndex = ((Id - 1) % PlaceholderPalette.Length + PlaceholderPalette.Length)
                               % PlaceholderPalette.Length;
            var (start, end) = PlaceholderPalette[paletteIndex];
            return CreatePlaceholderBrush(start, end);
        }
    }

    private static IBrush CreatePlaceholderBrush(string start, string end) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop(Color.Parse(start), 0),
            new GradientStop(Color.Parse(end), 1)
        }
    };
}