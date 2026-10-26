
using Avalonia;
using Avalonia.Controls;

namespace CineBook.Views;

public partial class MainView : NavigationPage
{
    public static MainView Current = null!;
    
    public MainView()
    {
        InitializeComponent();
        Current = this;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PushAsync(new HomePage());
    }
}