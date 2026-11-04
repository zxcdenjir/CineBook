using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CineBook.Models;
using CineBook.ViewModels;

namespace CineBook.Views;

public partial class MediaItemView : ContentPage
{
    public MediaItemView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is MediaItemViewModel viewModel)
            await viewModel.LoadRecommendationsAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is MediaItemViewModel viewModel)
            viewModel.DisposeRecommendations();

        base.OnDetachedFromVisualTree(e);
    }

    private async void OnRecommendationAddClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: OnlineMediaRecommendation recommendation } || recommendation.IsAdded)
            return;

        if (HomePage.Current.DataContext is HomePageViewModel viewModel)
            await viewModel.AddRecommendedMediaAsync(recommendation);
    }
}
