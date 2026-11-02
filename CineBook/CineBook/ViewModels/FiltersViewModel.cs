using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CineBook.Context;
using CineBook.Models;
using CineBook.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace CineBook.ViewModels;

public partial class FiltersViewModel : ViewModelBase
{
    [ObservableProperty] public partial int CountFilers { get; set; } = 0;

    public ObservableCollection<Genre> AllGenres { get; } = [];
    
    [ObservableProperty] public partial int SortBy { get; set; } = 0;
    [ObservableProperty] public partial int SortDirection { get; set; } = 0;
    

    public FiltersViewModel()
    {
        Load();
    }

    private async void Load()
    {
        if (Design.IsDesignMode) return;
        await using CineBookContext context = new();
        var genres = context.Genres.ToList();
        AllGenres.Clear();
        genres.ForEach(x => AllGenres.Add(x));
    }
}