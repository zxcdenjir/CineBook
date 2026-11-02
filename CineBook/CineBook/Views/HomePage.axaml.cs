using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CineBook.Views;

public partial class HomePage : ContentPage
{
    public static HomePage Current = null!;
    
    public HomePage()
    {
        InitializeComponent();
        Current = this;
    }
}