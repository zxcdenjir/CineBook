
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CineBook.ViewModels;
using Ursa.Controls;

namespace CineBook.Views;

public partial class MainView : NavigationPage
{
    public static MainView Current = null!;
    
    private WindowToastManager _toastManager = null!;
    
    public MainView()
    {
        InitializeComponent();
        Current = this;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        var topLevel = TopLevel.GetTopLevel(this)!;

        _toastManager = new WindowToastManager(topLevel)
        {
            Margin = new Thickness(0, EffectiveBarHeight, 0, 0),
        };

        PropertyChanged += OnMainViewPropertyChanged;
        PushAsync(new HomePage
        {
            DataContext = new HomePageViewModel()
        });
    }
    
    private void OnMainViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == EffectiveBarHeightProperty && e.NewValue is double height)
        {
            _toastManager.Margin = new Thickness(0, height, 0, 0);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        PropertyChanged -= OnMainViewPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void ShowToast(string message, NotificationType type)
    {
        _toastManager.Show(
            content: message,
            showIcon: true,
            showClose: true,
            type: type,
            expiration: TimeSpan.FromSeconds(3),
            classes: ["Light"]);
    }
}