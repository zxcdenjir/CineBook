using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CineBook.Context;
using CineBook.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace CineBook.ViewModels;

public enum MediaSortBy
{
    AdditionDate,
    Rating,
    Title
}

public enum MediaSortDirection
{
    Ascending,
    Descending
}

public partial class FiltersViewModel : ViewModelBase
{
    public ObservableCollection<Genre> AllGenres { get; } = [];

    [ObservableProperty] public partial MediaSortBy SortBy { get; set; } = MediaSortBy.AdditionDate;

    [ObservableProperty] public partial MediaSortDirection SortDirection { get; set; } = MediaSortDirection.Descending;

    public bool SortByDate
    {
        get => SortBy == MediaSortBy.AdditionDate;
        set { if (value) SortBy = MediaSortBy.AdditionDate; }
    }

    public bool SortByRating
    {
        get => SortBy == MediaSortBy.Rating;
        set { if (value) SortBy = MediaSortBy.Rating; }
    }

    public bool SortByTitle
    {
        get => SortBy == MediaSortBy.Title;
        set { if (value) SortBy = MediaSortBy.Title; }
    }

    public bool SortAscending
    {
        get => SortDirection == MediaSortDirection.Ascending;
        set { if (value) SortDirection = MediaSortDirection.Ascending; }
    }

    public bool SortDescending
    {
        get => SortDirection == MediaSortDirection.Descending;
        set { if (value) SortDirection = MediaSortDirection.Descending; }
    }

    public async Task LoadAsync()
    {
        if (Design.IsDesignMode)
            return;

        await using var context = new CineBookContext();
        var genres = await context.Genres.OrderBy(genre => genre.Name).ToListAsync();
        AllGenres.Clear();
        foreach (var genre in genres)
            AllGenres.Add(genre);
    }

    public void BeginEdit(IEnumerable<int> selectedGenreIds, MediaSortBy sortBy, MediaSortDirection sortDirection)
    {
        var selectedIds = selectedGenreIds.ToHashSet();
        foreach (var genre in AllGenres)
            genre.IsSelected = selectedIds.Contains(genre.Id);

        SortBy = sortBy;
        SortDirection = sortDirection;
    }

    public HashSet<int> GetSelectedGenreIds() => AllGenres
        .Where(genre => genre.IsSelected)
        .Select(genre => genre.Id)
        .ToHashSet();

    [RelayCommand]
    private void ClearFilters()
    {
        foreach (var genre in AllGenres)
            genre.IsSelected = false;

        SortBy = MediaSortBy.AdditionDate;
        SortDirection = MediaSortDirection.Descending;
    }

    partial void OnSortByChanged(MediaSortBy value)
    {
        OnPropertyChanged(nameof(SortByDate));
        OnPropertyChanged(nameof(SortByRating));
        OnPropertyChanged(nameof(SortByTitle));
    }

    partial void OnSortDirectionChanged(MediaSortDirection value)
    {
        OnPropertyChanged(nameof(SortAscending));
        OnPropertyChanged(nameof(SortDescending));
    }
}
