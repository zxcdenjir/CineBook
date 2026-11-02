using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using CineBook.Context;
using CineBook.Models;
using CineBook.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace CineBook.ViewModels;

public partial class HomePageViewModel : ViewModelBase
{
    private List<MediaItem> _mediaItems = null!;

    public ObservableCollection<MediaItem> AllMediaItems { get; } = [];
    public ObservableCollection<MediaItem> MoviesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> SeriesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> BooksMediaItems { get; } = [];
    public ObservableCollection<MediaItem> FavouriteMediaItems { get; } = [];

    private FiltersViewModel _filtersViewModel = null!;

    public HomePageViewModel()
    {
        _filtersViewModel = new FiltersViewModel();
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
                _mediaItems = await context.MediaItems.Include(x => x.Status).Include(x => x.Type)
                    .Include(x => x.Genres).ToListAsync();
                ApplyFilters();
            }
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(MediaItem item)
    {
        try
        {
            await using var context = new CineBookContext();
            var storedItem = await context.MediaItems.FindAsync(item.Id);
            if (storedItem is null)
                return;

            storedItem.IsFavorite = !storedItem.IsFavorite;
            await context.SaveChangesAsync();

            item.IsFavorite = storedItem.IsFavorite;
            if (item.IsFavorite)
            {
                if (!FavouriteMediaItems.Contains(item))
                    FavouriteMediaItems.Add(item);
            }
            else
            {
                FavouriteMediaItems.Remove(item);
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

    [RelayCommand]
    private async Task OpenFiltersAsync()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            await OverlayDrawer.ShowStandardAsync<FiltersView, FiltersViewModel>(_filtersViewModel, null,
                new DrawerOptions
                {
                    Buttons = DialogButton.OKCancel,
                    CanLightDismiss = true,
                    CanResize = false,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Position = Position.Bottom,
                    Title = "Фильтры и сортировка",
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                });
        }
        else
        {
            await OverlayDialog.ShowStandardAsync<FiltersView, FiltersViewModel>(_filtersViewModel, null,
                new OverlayDialogOptions()
                {
                    Buttons = DialogButton.OKCancel,
                    CanLightDismiss = true,
                    CanResize = false,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Title = "Фильтры и сортировка",
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    CanDragMove = true,
                    Mode = DialogMode.None
                });
        }
    }
}