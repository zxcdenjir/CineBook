using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CineBook.Models;

public sealed partial class OnlineMediaRecommendation(
    string title,
    string? creator,
    string? description,
    string posterUrl,
    int typeId,
    string[] genreNames,
    Bitmap poster) : ObservableObject, IDisposable
{
    public string Title { get; } = title;
    public string? Creator { get; } = creator;
    public string? Description { get; } = description;
    public string PosterUrl { get; } = posterUrl;
    public int TypeId { get; } = typeId;
    public string[] GenreNames { get; } = genreNames;
    public Bitmap Poster { get; } = poster;
    public bool HasCreator => !string.IsNullOrWhiteSpace(Creator);

    [ObservableProperty] public partial bool IsAdded { get; set; }

    public string AddButtonText => IsAdded ? "Добавлено" : "Добавить";

    partial void OnIsAddedChanged(bool value) => OnPropertyChanged(nameof(AddButtonText));

    public void Dispose() => Dispatcher.UIThread.Post(Poster.Dispose, DispatcherPriority.Background);
}

public static class OnlineMediaRecommendationService
{
    private const int MaxRecommendations = 10;
    private const int MaxPosterBytes = 2 * 1024 * 1024;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly IReadOnlyDictionary<string, GenreQuery> GenreQueries =
        new Dictionary<string, GenreQuery>(StringComparer.CurrentCultureIgnoreCase)
        {
            ["боевик"] = new("Action", "action"), ["action"] = new("Action", "action"),
            ["приключения"] = new("Adventure", "adventure"), ["adventure"] = new("Adventure", "adventure"),
            ["анимация"] = new("Animation", "animation"), ["мультфильм"] = new("Animation", "animation"),
            ["animation"] = new("Animation", "animation"),
            ["биография"] = new("Biography", "biography"), ["biography"] = new("Biography", "biography"),
            ["комедия"] = new("Comedy", "comedy"), ["comedy"] = new("Comedy", "comedy"),
            ["криминал"] = new("Crime", "crime"), ["crime"] = new("Crime", "crime"),
            ["документальный"] = new("Documentary", "documentary"),
            ["документальный фильм"] = new("Documentary", "documentary"),
            ["documentary"] = new("Documentary", "documentary"),
            ["драма"] = new("Drama", "drama"), ["drama"] = new("Drama", "drama"),
            ["семейный"] = new("Family", "family"), ["семейное"] = new("Family", "family"),
            ["family"] = new("Family", "family"),
            ["фэнтези"] = new("Fantasy", "fantasy"), ["fantasy"] = new("Fantasy", "fantasy"),
            ["история"] = new("History", "history"), ["исторический"] = new("History", "history"),
            ["history"] = new("History", "history"),
            ["ужасы"] = new("Horror", "horror"), ["хоррор"] = new("Horror", "horror"),
            ["horror"] = new("Horror", "horror"),
            ["детектив"] = new("Mystery", "detective_fiction"),
            ["мистика"] = new("Mystery", "mystery"), ["mystery"] = new("Mystery", "mystery"),
            ["мелодрама"] = new("Romance", "romance"), ["романтика"] = new("Romance", "romance"),
            ["romance"] = new("Romance", "romance"),
            ["фантастика"] = new("Sci-Fi", "science_fiction"),
            ["научная фантастика"] = new("Sci-Fi", "science_fiction"),
            ["sci-fi"] = new("Sci-Fi", "science_fiction"), ["science fiction"] = new("Sci-Fi", "science_fiction"),
            ["спорт"] = new("Sport", "sports"), ["sport"] = new("Sport", "sports"),
            ["триллер"] = new("Thriller", "thriller"), ["thriller"] = new("Thriller", "thriller"),
            ["военный"] = new("War", "war"), ["военное"] = new("War", "war"), ["war"] = new("War", "war"),
            ["вестерн"] = new("Western", "western"), ["western"] = new("Western", "western"),
            ["антиутопия"] = new("Sci-Fi", "dystopian"), ["dystopian"] = new("Sci-Fi", "dystopian")
        };

    static OnlineMediaRecommendationService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CineBook/1.0");
    }

    public static async Task<IReadOnlyList<OnlineMediaRecommendation>> FindAsync(
        MediaItem source,
        IReadOnlyCollection<string> savedTitles,
        CancellationToken cancellationToken = default)
    {
        var genreName = source.Genres.Select(item => item.Name.Trim()).FirstOrDefault(GenreQueries.ContainsKey);
        if (genreName is null)
            return [];

        var genre = GenreQueries[genreName];
        // shortcut: search by the first recognized genre; query additional genres if results are too narrow.
        var page = source.TypeId == 3
            ? await GetBooksBySubjectAsync(genre.BookSubject, cancellationToken)
            : await GetVideoCatalogAsync(source.TypeId == 1 ? "movie" : "series", genre.VideoGenre,
                cancellationToken);

        var knownTitles = savedTitles.Select(NormalizeTitle).ToHashSet(StringComparer.Ordinal);
        knownTitles.Add(NormalizeTitle(source.Title));
        var genreNames = new[] { genreName };
        var result = new List<OnlineMediaRecommendation>(MaxRecommendations);

        foreach (var candidate in page)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (knownTitles.Contains(NormalizeTitle(candidate.Title)) || !IsHttpsUrl(candidate.PosterUrl))
                    continue;

                var bytes = await DownloadPosterAsync(candidate.PosterUrl!, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new MemoryStream(bytes);
                var poster = new Bitmap(stream);
                result.Add(new OnlineMediaRecommendation(candidate.Title, candidate.Creator, candidate.Description,
                    candidate.PosterUrl!, source.TypeId, genreNames, poster));
                if (result.Count == MaxRecommendations)
                    break;
            }
            catch (OperationCanceledException)
            {
                foreach (var recommendation in result)
                    recommendation.Dispose();
                throw;
            }
            catch (Exception)
            {
                // One unavailable cover should not hide the other recommendations.
            }
        }

        return result;
    }

    private static async Task<List<Candidate>> GetVideoCatalogAsync(string type, string genre,
        CancellationToken cancellationToken)
    {
        var url = $"https://v3-cinemeta.strem.io/catalog/{type}/top/" +
                  $"genre={Uri.EscapeDataString(genre)}&skip=0.json";
        var page = await GetJsonAsync<CinemetaCatalogPage>(url, cancellationToken);
        return (page.Metas ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new Candidate(item.Name!, item.Directors?.FirstOrDefault(), item.Description, item.Poster))
            .ToList();
    }

    private static async Task<List<Candidate>> GetBooksBySubjectAsync(string subject,
        CancellationToken cancellationToken)
    {
        var url = $"https://openlibrary.org/subjects/{Uri.EscapeDataString(subject)}.json?limit=50";
        var page = await GetJsonAsync<OpenLibrarySubjectPage>(url, cancellationToken);
        return (page.Works ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new Candidate(item.Title!,
                item.Authors is { Count: > 0 } ? item.Authors[0].Name : null,
                null,
                item.CoverId is > 0 ? $"https://covers.openlibrary.org/b/id/{item.CoverId}-M.jpg?default=false" : null))
            .ToList();
    }

    private static async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Сервис рекомендаций вернул пустой ответ");
    }

    private static async Task<byte[]> DownloadPosterAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxPosterBytes)
            throw new InvalidDataException("Постер слишком большой");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (output.Length + read > MaxPosterBytes)
                throw new InvalidDataException("Постер слишком большой");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    // shortcut: de-duplicate by normalized title; persist provider IDs if translated aliases become an issue.
    private static string NormalizeTitle(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private sealed record GenreQuery(string VideoGenre, string BookSubject);
    private sealed record Candidate(string Title, string? Creator, string? Description, string? PosterUrl);

    private sealed class CinemetaCatalogPage
    {
        [JsonPropertyName("metas")] public List<CinemetaMedia>? Metas { get; set; } = [];
    }

    private sealed class CinemetaMedia
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("director")] public List<string>? Directors { get; set; }
        [JsonPropertyName("poster")] public string? Poster { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    private sealed class OpenLibrarySubjectPage
    {
        [JsonPropertyName("works")] public List<OpenLibraryWork>? Works { get; set; } = [];
    }

    private sealed class OpenLibraryWork
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("authors")] public List<OpenLibraryAuthor>? Authors { get; set; }
        [JsonPropertyName("cover_id")] public int? CoverId { get; set; }
    }

    private sealed class OpenLibraryAuthor
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
