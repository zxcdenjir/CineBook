using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CineBook.Context;
using CineBook.Models;
using CineBook.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace CineBook.ViewModels;

public partial class HomePageViewModel : ViewModelBase
{
    private const string LayoutSettingKey = "home.layout";
    // shortcut: imports are limited to 128 MiB and posters to 32 MiB; raise these for larger libraries.
    private const long MaxImportSize = 128 * 1024 * 1024;
    private const int MaxPosterSize = 32 * 1024 * 1024;
    private static readonly FilePickerFileType JsonFileType = new("CineBook JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private List<MediaItem> _mediaItems = null!;
    private HashSet<int> _selectedGenreIds = [];
    private MediaSortBy _sortBy = MediaSortBy.AdditionDate;
    private MediaSortDirection _sortDirection = MediaSortDirection.Descending;
    private readonly Task _loadTask;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private bool _settingsLoaded;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial int SelectedLayoutIndex { get; set; }

    public bool IsListView => SelectedLayoutIndex == 1;

    public int? SelectedGenreCount => _selectedGenreIds.Count > 0 ? _selectedGenreIds.Count : null;

    public ObservableCollection<MediaItem> AllMediaItems { get; } = [];
    public ObservableCollection<MediaItem> MoviesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> SeriesMediaItems { get; } = [];
    public ObservableCollection<MediaItem> BooksMediaItems { get; } = [];
    public ObservableCollection<MediaItem> FavouriteMediaItems { get; } = [];

    private FiltersViewModel _filtersViewModel = null!;

    public HomePageViewModel()
    {
        _filtersViewModel = new FiltersViewModel();
        _loadTask = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            if (!Design.IsDesignMode)
            {
                await using (var context = new CineBookContext())
                {
                    _mediaItems = await context.MediaItems.Include(x => x.Status).Include(x => x.Type)
                        .Include(x => x.Genres).ToListAsync();
                    var layoutSetting = await context.AppSettings.FindAsync(LayoutSettingKey);
                    if (int.TryParse(layoutSetting?.Value, out var layoutIndex) && layoutIndex is 0 or 1)
                        SelectedLayoutIndex = layoutIndex;
                }

                _settingsLoaded = true;

                await _filtersViewModel.LoadAsync();
                ApplyFilters();
            }
        }
        catch (Exception ex)
        {
            _settingsLoaded = true;
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
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
    }

    private void ApplyFilters()
    {
        if (_mediaItems is null)
            return;

        var searchText = SearchText.Trim();
        IEnumerable<MediaItem> filtered = _mediaItems;

        if (searchText.Length > 0)
        {
            filtered = filtered.Where(item =>
                item.Title.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                (item.Creator?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (item.Description?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }

        if (_selectedGenreIds.Count > 0)
            filtered = filtered.Where(item => item.Genres.Any(genre => _selectedGenreIds.Contains(genre.Id)));

        var items = SortItems(filtered).ToList();
        ReplaceItems(AllMediaItems, items);
        ReplaceItems(MoviesMediaItems, items.Where(item => item.TypeId == 1));
        ReplaceItems(SeriesMediaItems, items.Where(item => item.TypeId == 2));
        ReplaceItems(BooksMediaItems, items.Where(item => item.TypeId == 3));
        ReplaceItems(FavouriteMediaItems, items.Where(item => item.IsFavorite));
    }

    private IEnumerable<MediaItem> SortItems(IEnumerable<MediaItem> items) => _sortBy switch
    {
        MediaSortBy.Rating when _sortDirection == MediaSortDirection.Ascending => items
            .OrderBy(item => !item.Rating.HasValue).ThenBy(item => item.Rating),
        MediaSortBy.Rating => items
            .OrderBy(item => !item.Rating.HasValue).ThenByDescending(item => item.Rating),
        MediaSortBy.Title when _sortDirection == MediaSortDirection.Ascending => items
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
        MediaSortBy.Title => items
            .OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
        MediaSortBy.AdditionDate when _sortDirection == MediaSortDirection.Ascending => items
            .OrderBy(item => TryParseAdditionDate(item.AdditionDateTime) is null)
            .ThenBy(item => TryParseAdditionDate(item.AdditionDateTime)),
        _ => items.OrderBy(item => TryParseAdditionDate(item.AdditionDateTime) is null)
            .ThenByDescending(item => TryParseAdditionDate(item.AdditionDateTime))
    };

    private static DateTime? TryParseAdditionDate(string value) => DateTime.TryParseExact(
        value,
        "dd.MM.yyyy HH:mm",
        CultureInfo.GetCultureInfo("ru-RU"),
        DateTimeStyles.None,
        out var date)
        ? date
        : null;

    private static void ReplaceItems(ObservableCollection<MediaItem> target, IEnumerable<MediaItem> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            var storageProvider = TopLevel.GetTopLevel(MainView.Current)?.StorageProvider;
            if (storageProvider is null)
                throw new InvalidOperationException("Не удалось открыть окно сохранения файла");

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт CineBook",
                SuggestedFileName = "cinebook-export.json",
                FileTypeChoices = [JsonFileType],
                SuggestedFileType = JsonFileType,
                ShowOverwritePrompt = true
            });
            if (file is null)
                return;

            await using var context = new CineBookContext();
            var items = await context.MediaItems
                .Include(item => item.Type)
                .Include(item => item.Status)
                .Include(item => item.Genres)
                .OrderBy(item => item.Id)
                .ToListAsync();
            var archive = new MediaLibraryArchive();
            foreach (var item in items)
            {
                string? posterBase64 = null;
                if (!string.IsNullOrWhiteSpace(item.PosterPath) && File.Exists(item.PosterPath))
                {
                    if (new FileInfo(item.PosterPath).Length > MaxPosterSize)
                        throw new InvalidDataException($"Постер записи «{item.Title}» превышает ограничение 32 МиБ");
                    posterBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(item.PosterPath));
                }

                archive.Items.Add(new MediaLibraryArchiveItem
                {
                    Type = item.Type.Name,
                    Status = item.Status.Name,
                    Title = item.Title,
                    Creator = item.Creator ?? string.Empty,
                    Duration = item.Duration,
                    Rating = item.Rating,
                    Description = item.Description,
                    IsFavorite = item.IsFavorite,
                    AdditionDateTime = item.AdditionDateTime,
                    Genres = item.Genres.Select(genre => genre.Name).ToList(),
                    PosterExtension = item.PosterPath is null ? null : Path.GetExtension(item.PosterPath),
                    PosterBase64 = posterBase64
                });
            }

            await using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, archive, JsonOptions);
            MainView.Current.ShowToast("Коллекция экспортирована", NotificationType.Success);
        }
        catch (Exception exception)
        {
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var createdPosterPaths = new List<string>();
        try
        {
            var storageProvider = TopLevel.GetTopLevel(MainView.Current)?.StorageProvider;
            if (storageProvider is null)
                throw new InvalidOperationException("Не удалось открыть окно выбора файла");

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Импорт CineBook",
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType]
            });
            if (files.Count == 0)
                return;

            await using var input = await files[0].OpenReadAsync();
            await using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + read > MaxImportSize)
                    throw new InvalidDataException("Файл импорта превышает ограничение 128 МиБ");
                await buffer.WriteAsync(chunk.AsMemory(0, read));
            }

            buffer.Position = 0;
            var archive = await JsonSerializer.DeserializeAsync<MediaLibraryArchive>(buffer, JsonOptions)
                          ?? throw new InvalidDataException("Файл импорта пуст или повреждён");
            if (archive.Version != 1)
                throw new InvalidDataException("Версия файла импорта не поддерживается");
            if (archive.Items is null)
                throw new InvalidDataException("В файле импорта отсутствует список записей");

            await using var context = new CineBookContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var types = (await context.Types.ToListAsync())
                .ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);
            var statuses = (await context.Statuses.ToListAsync())
                .ToDictionary(status => status.Name, StringComparer.OrdinalIgnoreCase);
            var genres = (await context.Genres.ToListAsync())
                .ToDictionary(genre => genre.Name, StringComparer.OrdinalIgnoreCase);
            var existingItems = await context.MediaItems
                .Select(item => new { item.TypeId, item.Title, item.Creator })
                .ToListAsync();
            var existingKeys = existingItems
                .Select(item => BuildDuplicateKey(item.TypeId, item.Title, item.Creator))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var posterDirectory = Path.Combine(Path.GetDirectoryName(CineBookContext.GetDatabasePath())!, "posters");
            Directory.CreateDirectory(posterDirectory);
            var added = 0;
            var skipped = 0;

            foreach (var imported in archive.Items)
            {
                if (imported is null || imported.Genres is null)
                    throw new InvalidDataException("Файл импорта содержит некорректную запись");
                if (string.IsNullOrWhiteSpace(imported.Title) || string.IsNullOrWhiteSpace(imported.Creator))
                    throw new InvalidDataException("У каждой записи должны быть указаны название и автор/режиссёр");
                if (string.IsNullOrWhiteSpace(imported.Type) || string.IsNullOrWhiteSpace(imported.Status))
                    throw new InvalidDataException($"У записи «{imported.Title}» не указан тип или статус");
                if (!types.TryGetValue(imported.Type.Trim(), out var type) ||
                    !statuses.TryGetValue(imported.Status.Trim(), out var status))
                    throw new InvalidDataException($"Неизвестный тип или статус у записи «{imported.Title}»");
                if (imported.Duration < 0 || imported.Rating is < 0 or > 10 ||
                    (imported.Rating.HasValue && !double.IsFinite(imported.Rating.Value)))
                    throw new InvalidDataException($"Некорректные длительность или рейтинг у записи «{imported.Title}»");

                var title = imported.Title.Trim();
                var creator = imported.Creator.Trim();
                if (!existingKeys.Add(BuildDuplicateKey(type.Id, title, creator)))
                {
                    skipped++;
                    continue;
                }

                var itemGenres = new List<Genre>();
                var itemGenreNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var genreName in imported.Genres)
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                        continue;
                    var name = genreName.Trim();
                    if (!itemGenreNames.Add(name))
                        continue;
                    if (!genres.TryGetValue(name, out var genre))
                    {
                        genre = new Genre { Name = name };
                        genres.Add(name, genre);
                        context.Genres.Add(genre);
                    }
                    itemGenres.Add(genre);
                }

                string? posterPath = null;
                if (!string.IsNullOrWhiteSpace(imported.PosterBase64))
                {
                    var posterBytes = Convert.FromBase64String(imported.PosterBase64);
                    if (posterBytes.Length > MaxPosterSize)
                        throw new InvalidDataException($"Постер записи «{title}» превышает ограничение 32 МиБ");
                    var extension = imported.PosterExtension?.ToLowerInvariant();
                    if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif"))
                        throw new InvalidDataException($"Формат постера записи «{title}» не поддерживается");
                    try
                    {
                        using var posterStream = new MemoryStream(posterBytes);
                        using var poster = new Bitmap(posterStream);
                    }
                    catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
                    {
                        throw new InvalidDataException($"Постер записи «{title}» повреждён", exception);
                    }
                    posterPath = Path.Combine(posterDirectory, $"{Guid.NewGuid():N}{extension}");
                    createdPosterPaths.Add(posterPath);
                    await File.WriteAllBytesAsync(posterPath, posterBytes);
                }

                context.MediaItems.Add(new MediaItem
                {
                    TypeId = type.Id,
                    StatusId = status.Id,
                    Title = title,
                    Creator = creator,
                    Duration = imported.Duration ?? 0,
                    Rating = imported.Rating ?? 0,
                    Description = imported.Description,
                    IsFavorite = imported.IsFavorite,
                    AdditionDateTime = string.IsNullOrWhiteSpace(imported.AdditionDateTime)
                        ? DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"))
                        : imported.AdditionDateTime,
                    PosterPath = posterPath,
                    Genres = itemGenres
                });
                added++;
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            createdPosterPaths.Clear();
            await LoadAsync();
            MainView.Current.ShowToast($"Импортировано: {added}; пропущено повторов: {skipped}",
                NotificationType.Success);
        }
        catch (Exception exception)
        {
            foreach (var path in createdPosterPaths)
                TryDeleteFile(path);
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
        }
    }

    private static string BuildDuplicateKey(int typeId, string title, string? creator) =>
        $"{typeId}\u001f{title.Trim()}\u001f{creator?.Trim()}";

    [RelayCommand]
    private async Task AddMediaAsync() => await OpenMediaEditorAsync(new AddMediaViewModel(), "Добавить медиа");

    [RelayCommand]
    private async Task EditMediaAsync(MediaItem item) =>
        await OpenMediaEditorAsync(new AddMediaViewModel(item), "Изменить медиа");

    private async Task OpenMediaEditorAsync(AddMediaViewModel viewModel, string title)
    {
        try
        {
            await _loadTask;
            await viewModel.LoadAsync();

            while (true)
            {
                DialogResult result;
                if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
                {
                    result = await OverlayDrawer.ShowStandardAsync<AddMediaView, AddMediaViewModel>(viewModel, null,
                        new DrawerOptions
                        {
                            Buttons = DialogButton.OKCancel,
                            CanLightDismiss = true,
                            CanResize = false,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            IsCloseButtonVisible = true,
                            Position = Position.Bottom,
                            Title = title,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            StyleClass = "AddMedia"
                        });
                }
                else
                {
                    result = await OverlayDialog.ShowStandardAsync<AddMediaView, AddMediaViewModel>(viewModel, null,
                        new OverlayDialogOptions
                        {
                            Buttons = DialogButton.OKCancel,
                            CanLightDismiss = true,
                            CanResize = false,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            IsCloseButtonVisible = true,
                            Title = title,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            CanDragMove = true,
                            Mode = DialogMode.None,
                            StyleClass = "AddMedia"
                        });
                }

                if (viewModel.WasDeleted)
                {
                    await LoadAsync();
                    MainView.Current.ShowToast("Медиа удалено", NotificationType.Success);
                    return;
                }

                if (result != DialogResult.OK)
                {
                    viewModel.DiscardPendingPosters();
                    return;
                }

                if (await viewModel.SaveAsync())
                {
                    await LoadAsync();
                    MainView.Current.ShowToast(viewModel.IsEditing ? "Изменения сохранены" : "Медиа добавлено",
                        NotificationType.Success);
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            viewModel.DiscardPendingPosters();
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
        }
        finally
        {
            viewModel.DisposeCoverSearch();
        }
    }

    [RelayCommand]
    private async Task OpenFiltersAsync()
    {
        await _loadTask;
        _filtersViewModel.BeginEdit(_selectedGenreIds, _sortBy, _sortDirection);

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            var result = await OverlayDrawer.ShowStandardAsync<FiltersView, FiltersViewModel>(_filtersViewModel, null,
                new DrawerOptions
                {
                    Buttons = DialogButton.OKCancel,
                    CanLightDismiss = true,
                    CanResize = false,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Position = Position.Bottom,
                    Title = "Фильтры и сортировка",
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    StyleClass = "AddMedia"
                });
            if (result == DialogResult.OK)
                ApplyDialogFilters();
        }
        else
        {
            var result = await OverlayDialog.ShowStandardAsync<FiltersView, FiltersViewModel>(_filtersViewModel, null,
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
            if (result == DialogResult.OK)
                ApplyDialogFilters();
        }
    }

    private void ApplyDialogFilters()
    {
        _selectedGenreIds = _filtersViewModel.GetSelectedGenreIds();
        _sortBy = _filtersViewModel.SortBy;
        _sortDirection = _filtersViewModel.SortDirection;
        OnPropertyChanged(nameof(SelectedGenreCount));
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedLayoutIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsListView));
        if (_settingsLoaded)
            _ = SaveLayoutSettingAsync();
    }

    private async Task SaveLayoutSettingAsync()
    {
        await _settingsLock.WaitAsync();
        try
        {
            await using var context = new CineBookContext();
            var setting = await context.AppSettings.FindAsync(LayoutSettingKey);
            if (setting is null)
            {
                setting = new AppSetting { Key = LayoutSettingKey };
                context.AppSettings.Add(setting);
            }
            setting.Value = SelectedLayoutIndex.ToString(CultureInfo.InvariantCulture);
            await context.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message, NotificationType.Error);
        }
        finally
        {
            _settingsLock.Release();
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
    }
}
