using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CineBook.Context;
using CineBook.Models;
using CineBook.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace CineBook.ViewModels;

public partial class HomePageViewModel : ViewModelBase
{
    private List<MediaItem> _mediaItems = null!;
    
    public ObservableCollection<MediaItem> AllMediaItems { get; } = [];
    public ObservableCollection<MediaItem> MoviesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> SeriesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> BooksMediaItems { get; } = [];
    public ObservableCollection<MediaItem> FavouriteMediaItems { get; } = [];

    public HomePageViewModel()
    {
        _ = LoadAsync();
    }
    
    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            if (!Design.IsDesignMode)
            {
                await using var context = new CineBookContext();
                _mediaItems = await context.MediaItems.Include(x => x.Status).Include(x => x.Type).Include(x => x.Genres).ToListAsync();
                ApplyFilters();
            }
           
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
    }
    
    private void ApplyFilters()
    {
        AllMediaItems.Clear();
        _mediaItems.ForEach(item => AllMediaItems.Add(item));
        MoviesMediaItems.Clear();
        _mediaItems.FindAll(item => item.TypeId == 1).ForEach(item => MoviesMediaItems.Add(item));
        SeriesMediaItems.Clear();
        _mediaItems.FindAll(item => item.TypeId == 2).ForEach(item => SeriesMediaItems.Add(item));
        BooksMediaItems.Clear();
        _mediaItems.FindAll(item => item.TypeId == 3).ForEach(item => BooksMediaItems.Add(item));
        FavouriteMediaItems.Clear();
        _mediaItems.FindAll(item => item.IsFavorite).ForEach(item => FavouriteMediaItems.Add(item));
    }
}