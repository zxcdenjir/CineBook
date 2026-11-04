using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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
    private const int CoverPageSize = 10;
    private const int CoverSourcePageSize = 100;
    private const int MaxThumbnailBytes = 5 * 1024 * 1024;
    private const int MaxDownloadedCoverBytes = 32 * 1024 * 1024;
    private const string LegacyTmdbAccessTokenSettingKey = "cover-search.tmdb-token";
    private const string LegacyGoogleBooksApiKeySettingKey = "cover-search.google-books-key";
    private static readonly HttpClient CoverImageHttpClient = new() { Timeout = TimeSpan.FromSeconds(25) };

    private readonly int? _mediaItemId;
    private readonly HashSet<int> _selectedGenreIds = [];
    private readonly HashSet<string> _pendingPosterPaths = [];
    private readonly string? _originalPosterPath;
    private readonly List<CoverImageCandidate> _coverCandidates = [];
    private CancellationTokenSource? _coverSearchCancellation;
    private CancellationTokenSource? _coverSelectionCancellation;
    private TaskCompletionSource<bool>? _coverSelectionCompletion;
    private int _coverSearchGeneration;
    private int _coverSelectionGeneration;
    private int _coverLoadedCount;
    private int _coverSourceOffset;
    private bool _hasMoreCoverSource;
    private bool _hasSearchedCovers;
    private string? _activeCoverQuery;
    private string? _selectedCoverImageUrl;
    private CoverImageResult? _selectedCoverImage;

    static AddMediaViewModel()
    {
        CoverImageHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CineBook/1.0");
    }

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
        PosterPath = File.Exists(mediaItem.PosterPath) ? mediaItem.PosterPath : null;
    }

    public ObservableCollection<Genre> AllGenres { get; } = [];
    public ObservableCollection<Status> AllStatuses { get; } = [];

    [ObservableProperty] public partial int SelectedMediaTypeId { get; set; }

    [Required(ErrorMessage = "Заполните название")]
    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    [Required(ErrorMessage = "Заполните создателя")]
    public partial string? Creator { get; set; }

    [ObservableProperty] public partial string? Description { get; set; }

    [ObservableProperty]
    [Required(ErrorMessage = "Заполните продолжительность")]
    public partial int? Duration { get; set; }

    [ObservableProperty]
    [Required(ErrorMessage = "Заполните рейтинг")]
    public partial double? Rating { get; set; }
    [ObservableProperty] public partial int SelectedStatusId { get; set; }
    [ObservableProperty] public partial bool IsFavorite { get; set; }
    [ObservableProperty] public partial string? PosterPath { get; set; }
    [ObservableProperty] public partial bool IsSearchingCovers { get; set; }
    [ObservableProperty] public partial bool IsSelectingCover { get; set; }
    [ObservableProperty] public partial string? CoverSearchError { get; set; }

    public ObservableCollection<CoverImageResult> CoverImageResults { get; } = [];
    public bool CanSearchCovers => !string.IsNullOrWhiteSpace(Name);
    public bool HasCoverSearchError => !string.IsNullOrWhiteSpace(CoverSearchError);
    public bool IsBook => SelectedMediaTypeId == 2;
    public string CoverResultsHeader => "Изображения по названию";

    public bool HasNoCoverImages => CanSearchCovers && _hasSearchedCovers && CoverImageResults.Count == 0 &&
                                    !IsSearchingCovers && !HasCoverSearchError && !HasMoreCoverImages;

    public bool HasMoreCoverImages => _coverLoadedCount < _coverCandidates.Count ||
                                      _hasMoreCoverSource;

    public Bitmap? SelectedImage
    {
        get => null;
        set
        {
            if (value is null)
                return;

            try
            {
                CancelPendingCoverSelection();
                SavePoster(value);
                _selectedCoverImageUrl = null;
                SetSelectedCoverImage(null);
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
        var legacyCoverKeys = await context.AppSettings.Where(setting =>
                setting.Key == LegacyTmdbAccessTokenSettingKey || setting.Key == LegacyGoogleBooksApiKeySettingKey)
            .ToListAsync();
        if (legacyCoverKeys.Count > 0)
        {
            context.AppSettings.RemoveRange(legacyCoverKeys);
            await context.SaveChangesAsync();
        }

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
            SelectedStatusId =
                statuses.FirstOrDefault(status => status.Name.Contains("Планирую", StringComparison.OrdinalIgnoreCase))
                    ?.Id
                ?? statuses.FirstOrDefault()?.Id
                ?? 0;
    }

    public async Task<bool> SaveAsync()
    {
        var pendingSelection = _coverSelectionCompletion;
        if (pendingSelection is not null && !await pendingSelection.Task)
            return false;

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

    partial void OnSelectedMediaTypeIdChanged(int value)
    {
        OnPropertyChanged(nameof(CreatorLabel));
        OnPropertyChanged(nameof(IsBook));
        OnPropertyChanged(nameof(CoverResultsHeader));
        ResetCoverSearch();
    }

    partial void OnSelectedStatusIdChanged(int value) => OnPropertyChanged(nameof(SelectedStatus));

    partial void OnCoverSearchErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCoverSearchError));
        OnPropertyChanged(nameof(HasNoCoverImages));
    }

    partial void OnIsSearchingCoversChanged(bool value) => OnPropertyChanged(nameof(HasNoCoverImages));

    partial void OnNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CanSearchCovers));
        ResetCoverSearch();
    }

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

    [RelayCommand]
    private async Task LoadMoreCoverImagesAsync()
    {
        if (IsSearchingCovers || !HasMoreCoverImages || _coverSearchCancellation is null)
            return;

        IsSearchingCovers = true;
        CoverSearchError = null;
        var generation = _coverSearchGeneration;
        var token = _coverSearchCancellation.Token;
        try
        {
            if (_coverLoadedCount >= _coverCandidates.Count && _hasMoreCoverSource)
            {
                if (IsBook)
                    await LoadOpenLibraryCandidatesAsync(_coverSourceOffset, generation, token);
                else
                    await LoadCinemetaCandidatesAsync(_activeCoverQuery!, _coverSourceOffset, generation, token);
            }

            await LoadNextCoverBatchAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == _coverSearchGeneration)
                CoverSearchError = GetCoverSearchError(exception);
        }
        finally
        {
            if (generation == _coverSearchGeneration)
                IsSearchingCovers = false;
        }
    }

    [RelayCommand]
    private async Task SelectCoverImageAsync(CoverImageResult? image)
    {
        if (image is null)
            return;

        CancelPendingCoverSelection();
        var generation = _coverSelectionGeneration;
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _coverSelectionCancellation = cancellation;
        _coverSelectionCompletion = completion;
        IsSelectingCover = true;
        var token = cancellation.Token;
        var selected = false;
        try
        {
            var bytes = await DownloadImageBytesAsync(image.ImageUrl, MaxDownloadedCoverBytes, token);
            if (generation != _coverSelectionGeneration)
                return;

            using var stream = new MemoryStream(bytes);
            using var bitmap = new Bitmap(stream);
            SavePoster(bitmap);
            _selectedCoverImageUrl = image.ImageUrl;
            SetSelectedCoverImage(image);
            selected = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            MainView.Current.ShowToast(exception.Message, NotificationType.Error);
        }
        finally
        {
            if (ReferenceEquals(_coverSelectionCancellation, cancellation))
            {
                _coverSelectionCancellation = null;
                _coverSelectionCompletion = null;
                IsSelectingCover = false;
            }

            completion.TrySetResult(selected);
        }
    }

    public void DisposeCoverSearch()
    {
        _coverSearchCancellation?.Cancel();
        _coverSearchCancellation = null;
        CancelPendingCoverSelection();
        _selectedCoverImageUrl = null;
        SetSelectedCoverImage(null);
        _coverCandidates.Clear();
        ClearCoverImageResults();
        IsSearchingCovers = false;
    }

    private void ResetCoverSearch()
    {
        _coverSearchCancellation?.Cancel();
        _coverSearchCancellation = null;
        CancelPendingCoverSelection();
        SetSelectedCoverImage(null);
        _coverSearchGeneration++;
        _activeCoverQuery = null;
        _coverCandidates.Clear();
        _coverLoadedCount = 0;
        _coverSourceOffset = 0;
        _hasMoreCoverSource = false;
        _hasSearchedCovers = false;
        CoverSearchError = null;
        ClearCoverImageResults();
        OnPropertyChanged(nameof(HasMoreCoverImages));
        OnPropertyChanged(nameof(HasNoCoverImages));
        IsSearchingCovers = false;
    }

    [RelayCommand]
    private async Task SearchCoverImagesAsync()
    {
        if (Design.IsDesignMode || !CanSearchCovers || IsSearchingCovers)
            return;

        ResetCoverSearch();
        var title = Name!.Trim();
        title = title[..Math.Min(title.Length, 200)];
        var cancellation = new CancellationTokenSource();
        _coverSearchCancellation = cancellation;
        var token = cancellation.Token;
        var generation = _coverSearchGeneration;
        IsSearchingCovers = true;
        try
        {
            _activeCoverQuery = title;
            if (IsBook)
                await LoadOpenLibraryCandidatesAsync(0, generation, token);
            else
                await LoadCinemetaCandidatesAsync(title, 0, generation, token);

            if (generation == _coverSearchGeneration)
            {
                _hasSearchedCovers = true;
                OnPropertyChanged(nameof(HasNoCoverImages));
                await LoadNextCoverBatchAsync(generation, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == _coverSearchGeneration)
                CoverSearchError = GetCoverSearchError(exception);
        }
        finally
        {
            if (generation == _coverSearchGeneration)
                IsSearchingCovers = false;
        }
    }

    private async Task LoadCinemetaCandidatesAsync(string title, int startIndex, int generation,
        CancellationToken token)
    {
        var mediaType = SelectedMediaTypeId == 0 ? "movie" : "series";
        var uri = $"https://v3-cinemeta.strem.io/catalog/{mediaType}/top/" +
                  $"search={Uri.EscapeDataString(title)}&skip={startIndex}.json";
        var resultPage = await GetJsonAsync<CinemetaCatalogPage>(uri, token);
        var items = resultPage.Metas ?? [];
        _coverSourceOffset = startIndex + items.Count;
        _hasMoreCoverSource = items.Count == CoverSourcePageSize;

        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            if (generation != _coverSearchGeneration || !IsHttpsUrl(item.Poster))
                continue;

            _coverCandidates.Add(new CoverImageCandidate(item.Poster!, item.Name ?? title));
        }

        OnPropertyChanged(nameof(HasMoreCoverImages));
    }

    private async Task LoadOpenLibraryCandidatesAsync(int startIndex, int generation, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_activeCoverQuery))
            return;

        var uri = "https://openlibrary.org/search.json" +
                  $"?title={Uri.EscapeDataString(_activeCoverQuery)}&limit={CoverSourcePageSize}&offset={startIndex}" +
                  "&fields=title,author_name,cover_i";
        var resultPage = await GetJsonAsync<OpenLibrarySearchPage>(uri, token);
        var items = resultPage.Docs ?? [];
        _coverSourceOffset = startIndex + items.Count;
        _hasMoreCoverSource = items.Count > 0 && _coverSourceOffset < resultPage.NumFound;

        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            if (generation != _coverSearchGeneration)
                return;

            if (item.CoverId is not > 0)
                continue;

            var imageUrl = $"https://covers.openlibrary.org/b/id/{item.CoverId}-M.jpg?default=false";
            var displayName = item.Title ?? _activeCoverQuery;
            if (item.Authors is { Count: > 0 })
                displayName += $" · {string.Join(", ", item.Authors)}";
            _coverCandidates.Add(new CoverImageCandidate(imageUrl, displayName));
        }

        OnPropertyChanged(nameof(HasMoreCoverImages));
    }

    private async Task LoadNextCoverBatchAsync(int generation, CancellationToken token)
    {
        var end = Math.Min(_coverLoadedCount + CoverPageSize, _coverCandidates.Count);
        while (_coverLoadedCount < end)
        {
            token.ThrowIfCancellationRequested();
            var candidate = _coverCandidates[_coverLoadedCount++];
            try
            {
                var bytes = await DownloadImageBytesAsync(candidate.ImageUrl, MaxThumbnailBytes, token);
                using var imageStream = new MemoryStream(bytes);
                var thumbnail = new Bitmap(imageStream);
                if (generation != _coverSearchGeneration)
                {
                    thumbnail.Dispose();
                    return;
                }

                var result = new CoverImageResult(thumbnail, candidate.ImageUrl, candidate.DisplayName);
                CoverImageResults.Add(result);
                if (string.Equals(candidate.ImageUrl, _selectedCoverImageUrl, StringComparison.Ordinal))
                    SetSelectedCoverImage(result);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A dead or unsupported cover should not hide the remaining search results.
            }
        }

        OnPropertyChanged(nameof(HasMoreCoverImages));
        OnPropertyChanged(nameof(HasNoCoverImages));
    }

    private static async Task<T> GetJsonAsync<T>(string url, CancellationToken token)
    {
        using var response = await CoverImageHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(token);
        return await JsonSerializer.DeserializeAsync<T>(responseStream, cancellationToken: token)
               ?? throw new InvalidDataException("Сервис поиска вернул пустой ответ");
    }

    private static async Task<byte[]> DownloadImageBytesAsync(string url, int maxBytes, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Ссылка на изображение должна использовать HTTPS");

        using var response = await CoverImageHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException("Файл изображения слишком большой");

        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (output.Length + read > maxBytes)
                throw new InvalidDataException("Файл изображения слишком большой");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }

        return output.ToArray();
    }

    private void SavePoster(Bitmap bitmap)
    {
        var posterDirectory = Path.Combine(Path.GetDirectoryName(CineBookContext.GetDatabasePath())!, "posters");
        Directory.CreateDirectory(posterDirectory);
        var path = Path.Combine(posterDirectory, $"{Guid.NewGuid():N}.png");
        _pendingPosterPaths.Add(path);
        using (var stream = File.Create(path))
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        PosterPath = path;
    }

    private void ClearCoverImageResults()
    {
        SetSelectedCoverImage(null);
        foreach (var result in CoverImageResults)
            result.Dispose();
        CoverImageResults.Clear();
    }

    private void CancelPendingCoverSelection()
    {
        _coverSelectionGeneration++;
        var cancellation = _coverSelectionCancellation;
        _coverSelectionCancellation = null;
        cancellation?.Cancel();
        _coverSelectionCompletion?.TrySetResult(false);
        _coverSelectionCompletion = null;
        IsSelectingCover = false;
    }

    private void SetSelectedCoverImage(CoverImageResult? image)
    {
        if (_selectedCoverImage is not null)
            _selectedCoverImage.IsSelected = false;

        _selectedCoverImage = image;
        if (_selectedCoverImage is not null)
            _selectedCoverImage.IsSelected = true;
    }

    private static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static string GetCoverSearchError(Exception exception) =>
        exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
            ? "Сервис поиска временно ограничил запросы. Попробуйте позже."
            : "Не удалось найти изображения. Проверьте подключение и попробуйте ещё раз.";

    private sealed record CoverImageCandidate(string ImageUrl, string DisplayName);

    private sealed class CinemetaCatalogPage
    {
        [JsonPropertyName("metas")] public List<CinemetaMedia>? Metas { get; set; } = [];
    }

    private sealed class CinemetaMedia
    {
        [JsonPropertyName("poster")] public string? Poster { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class OpenLibrarySearchPage
    {
        [JsonPropertyName("numFound")] public int NumFound { get; set; }
        [JsonPropertyName("docs")] public List<OpenLibraryBook>? Docs { get; set; } = [];
    }

    private sealed class OpenLibraryBook
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("author_name")] public List<string>? Authors { get; set; }
        [JsonPropertyName("cover_i")] public int? CoverId { get; set; }
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
