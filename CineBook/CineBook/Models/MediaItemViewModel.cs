using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CineBook.Context;
using CineBook.ViewModels;
using CineBook.Views;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Models;

public partial class MediaItemViewModel(MediaItem item) : ViewModelBase
{
    private bool _recommendationsLoaded;
    private CancellationTokenSource? _recommendationCancellation;

    [ObservableProperty] public partial bool IsLoadingRecommendations { get; set; }

    public MediaItem MediaItem { get; set; } = item;

    public bool HasDescription => !string.IsNullOrWhiteSpace(MediaItem.Description);

    public ObservableCollection<OnlineMediaRecommendation> Recommendations { get; } = [];

    public bool HasRecommendations => Recommendations.Count > 0;

    public async Task LoadRecommendationsAsync()
    {
        if (_recommendationsLoaded || Design.IsDesignMode)
            return;

        _recommendationsLoaded = true;
        var cancellation = new CancellationTokenSource();
        _recommendationCancellation = cancellation;
        IsLoadingRecommendations = true;

        try
        {
            await using var context = new CineBookContext();
            var savedTitles = await context.MediaItems
                .Where(candidate => candidate.TypeId == MediaItem.TypeId)
                .Select(candidate => candidate.Title)
                .ToListAsync();

            var recommendations = await OnlineMediaRecommendationService.FindAsync(MediaItem, savedTitles,
                cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                foreach (var recommendation in recommendations)
                    recommendation.Dispose();
                return;
            }

            foreach (var recommendation in recommendations)
                Recommendations.Add(recommendation);

            OnPropertyChanged(nameof(HasRecommendations));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (MainView.Current is not null)
                MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message,
                    NotificationType.Warning);
        }
        finally
        {
            if (ReferenceEquals(_recommendationCancellation, cancellation))
            {
                _recommendationCancellation = null;
                IsLoadingRecommendations = false;
            }
            cancellation.Dispose();
        }
    }

    public void DisposeRecommendations()
    {
        _recommendationCancellation?.Cancel();
        _recommendationCancellation = null;
        IsLoadingRecommendations = false;
        foreach (var recommendation in Recommendations)
            recommendation.Dispose();

        Recommendations.Clear();
        OnPropertyChanged(nameof(HasRecommendations));
        _recommendationsLoaded = false;
    }
}
