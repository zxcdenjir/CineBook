using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CineBook.Models;
using CineBook.ViewModels;

namespace CineBook.Views;

public partial class HomePage : ContentPage
{
    public static HomePage Current = null!;
    
    public HomePage()
    {
        InitializeComponent();
        Current = this;
    }

    private async void OnMediaItemTapped(object? sender, TappedEventArgs e)
    {
        MediaItem? tappedItem = null;
        for (var control = e.Source as Control; control is not null; control = control.GetVisualParent() as Control)
        {
            if (control is Button)
            {
                e.Handled = true;
                return;
            }

            if (control.DataContext is MediaItem item)
                tappedItem = item;
        }

        if (tappedItem is null)
            return;

        e.Handled = true;
        await MainView.Current.PushAsync(new MediaItemView
        {
            DataContext = new MediaItemViewModel(tappedItem)
        });
    }
}
