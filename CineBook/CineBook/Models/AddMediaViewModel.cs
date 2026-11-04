using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using CineBook.Context;
using CineBook.Views;
using Irihi.Avalonia.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ursa.Common;
using Ursa.Controls;

namespace CineBook.Models;

public partial class AddMediaViewModel : ObservableValidator, IDialogContext
{
    private readonly int? _mediaItemId;
    private readonly HashSet<int> _selectedGenreIds = [];
    private readonly HashSet<string> _pendingPosterPaths = [];
    private readonly string? _originalPosterPath;

    public event EventHandler<object?>? RequestClose;

    public bool IsEditing => _mediaItemId.HasValue;
    public bool WasDeleted { get; private set; }

    public AddMediaViewModel()
    {
    }

    public AddMediaViewModel(MediaItem mediaItem)
    {
        _mediaItemId = mediaItem.Id;
        _originalPosterPath = mediaItem.PosterPath;
        _selectedGenreIds = mediaItem.Genres.Select(genre => genre.Id).ToHashSet();

        SelectedMediaTypeId = mediaItem.TypeId - 1;
        Name = mediaItem.Title;
        Creator = mediaItem.Creator;
        Description = mediaItem.Description;
        Duration = mediaItem.Duration ?? 0;
        Rating = mediaItem.Rating ?? 0;
        SelectedStatusId = mediaItem.StatusId;
        IsFavorite = mediaItem.IsFavorite;
        PosterPath = mediaItem.PosterPath;

    }

    public ObservableCollection<Genre> AllGenres { get; } = [];
    public ObservableCollection<Status> AllStatuses { get; } = [];

    [ObservableProperty] public partial int SelectedMediaTypeId { get; set; }

    [Required(ErrorMessage = "Заполните название")]
    [ObservableProperty] public partial string? Name { get; set; }

    [ObservableProperty] [Required(ErrorMessage = "Заполните создателя")] public partial string? Creator { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial int? Duration { get; set; }
    [ObservableProperty] public partial double? Rating { get; set; }
    [ObservableProperty] public partial int SelectedStatusId { get; set; }
    [ObservableProperty] public partial bool IsFavorite { get; set; }
    [ObservableProperty] public partial string? PosterPath { get; set; }

    public Bitmap? SelectedImage
    {
        get => null;
        set
        {
            if (value is null)
                return;

            try
            {
                var posterDirectory = Path.Combine(Path.GetDirectoryName(CineBookContext.GetDatabasePath())!, "posters");
                Directory.CreateDirectory(posterDirectory);
                var path = Path.Combine(posterDirectory, $"{Guid.NewGuid():N}.png");
                _pendingPosterPaths.Add(path);
                using var stream = File.Create(path);
                value.Save(stream, PngBitmapEncoderOptions.Default);
                PosterPath = path;
            }
            catch (Exception exception)
            {
                MainView.Current.ShowToast(exception.Message, NotificationType.Error);
            }
        }
    }

    public string CreatorLabel => SelectedMediaTypeId == 2 ? "Автор" : "Режиссёр";
    public Status? SelectedStatus
    {
        get => AllStatuses.FirstOrDefault(status => status.Id == SelectedStatusId);
        set => SelectedStatusId = value?.Id ?? 0;
    }

    public async Task LoadAsync()
    {
        if (Design.IsDesignMode)
            return;

        await using var context = new CineBookContext();
        var genres = await context.Genres.OrderBy(genre => genre.Name).ToListAsync();
        var statuses = await context.Statuses.OrderBy(status => status.Id).ToListAsync();

        AllGenres.Clear();
        foreach (var genre in genres)
        {
            genre.IsSelected = _selectedGenreIds.Contains(genre.Id);
            AllGenres.Add(genre);
        }

        AllStatuses.Clear();
        foreach (var status in statuses)
            AllStatuses.Add(status);

        OnPropertyChanged(nameof(SelectedStatus));
        if (SelectedStatusId == 0)
            SelectedStatusId = statuses.FirstOrDefault(status => status.Name.Contains("Планирую", StringComparison.OrdinalIgnoreCase))?.Id
                               ?? statuses.FirstOrDefault()?.Id
                               ?? 0;
    }

    public async Task<bool> SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors || SelectedStatusId == 0)
        {
            var message = SelectedStatusId == 0
                ? "Выберите статус"
                : "Проверьте обязательные поля";
            MainView.Current.ShowToast(message, NotificationType.Warning);
            return false;
        }

        try
        {
            await using var context = new CineBookContext();
            MediaItem mediaItem;
            if (_mediaItemId is int id)
            {
                mediaItem = await context.MediaItems.Include(item => item.Genres)
                    .SingleAsync(item => item.Id == id);
            }
            else
            {
                mediaItem = new MediaItem
                {
                    AdditionDateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"))
                };
                context.MediaItems.Add(mediaItem);
            }

            mediaItem.TypeId = SelectedMediaTypeId + 1;
            mediaItem.Title = Name!.Trim();
            mediaItem.Creator = Creator?.Trim();
            mediaItem.Description = Description?.Trim();
            mediaItem.Duration = Duration;
            mediaItem.Rating = Rating;
            mediaItem.StatusId = SelectedStatusId;
            mediaItem.IsFavorite = IsFavorite;
            mediaItem.PosterPath = await SavePosterAsync();

            var selectedGenreIds = AllGenres.Where(genre => genre.IsSelected).Select(genre => genre.Id).ToArray();
            var selectedGenres = selectedGenreIds.Length == 0
                ? []
                : await context.Genres.Where(genre => selectedGenreIds.Contains(genre.Id)).ToListAsync();
            mediaItem.Genres.Clear();
            foreach (var genre in selectedGenres)
                mediaItem.Genres.Add(genre);

            await context.SaveChangesAsync();
            DeletePendingPostersExcept(mediaItem.PosterPath);
            if (!PathEquals(_originalPosterPath, mediaItem.PosterPath))
                DeleteManagedPoster(_originalPosterPath);
            return true;
        }
        catch (Exception exception)
        {
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
            return false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_mediaItemId is not int id)
            return;

        try
        {
            var result = await OverlayMessageBox.ShowAsync(
                "Удалить эту запись из коллекции?",
                "Удаление медиа",
                null,
                MessageBoxIcon.Question,
                button: MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK)
                return;

            await using var context = new CineBookContext();
            var mediaItem = await context.MediaItems.SingleAsync(item => item.Id == id);
            var posterPath = mediaItem.PosterPath;
            context.MediaItems.Remove(mediaItem);
            await context.SaveChangesAsync();

            DeleteManagedPoster(posterPath);
            DiscardPendingPosters();
            WasDeleted = true;
            RequestClose?.Invoke(this, DialogResult.OK);
        }
        catch (Exception exception)
        {
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
        }
    }

    public void Close() => RequestClose?.Invoke(this, DialogResult.Cancel);

    public void DiscardPendingPosters()
    {
        foreach (var path in _pendingPosterPaths)
            TryDeleteFile(path);
        _pendingPosterPaths.Clear();
    }

    partial void OnSelectedMediaTypeIdChanged(int value) => OnPropertyChanged(nameof(CreatorLabel));

    partial void OnSelectedStatusIdChanged(int value) => OnPropertyChanged(nameof(SelectedStatus));

    private async Task<string?> SavePosterAsync()
    {
        if (string.IsNullOrWhiteSpace(PosterPath) || PathEquals(PosterPath, _originalPosterPath))
            return PosterPath;
        if (!File.Exists(PosterPath))
            throw new FileNotFoundException("Выбранный файл постера больше не найден", PosterPath);

        var posterDirectory = Path.Combine(Path.GetDirectoryName(CineBookContext.GetDatabasePath())!, "posters");
        Directory.CreateDirectory(posterDirectory);
        if (IsManagedPosterPath(PosterPath))
            return PosterPath;

        var path = Path.Combine(posterDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(PosterPath)}");
        _pendingPosterPaths.Add(path);
        File.Copy(PosterPath, path);
        PosterPath = path;
        return path;
    }

    private void DeletePendingPostersExcept(string? retainedPath)
    {
        foreach (var path in _pendingPosterPaths)
        {
            if (!PathEquals(path, retainedPath))
                TryDeleteFile(path);
        }
        _pendingPosterPaths.Clear();
    }

    private static bool IsManagedPosterPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var posterDirectory = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(CineBookContext.GetDatabasePath())!, "posters"));
            return string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), posterDirectory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void DeleteManagedPoster(string? path)
    {
        if (IsManagedPosterPath(path))
            TryDeleteFile(path!);
    }

    private static bool PathEquals(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            return string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second);

        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return string.Equals(first, second, StringComparison.Ordinal);
        }
        catch (NotSupportedException)
        {
            return string.Equals(first, second, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return string.Equals(first, second, StringComparison.Ordinal);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
