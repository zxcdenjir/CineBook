using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CineBook.Controls;

public class PosterImage : Image
{
    public static readonly StyledProperty<string?> PosterPathProperty =
        AvaloniaProperty.Register<PosterImage, string?>(nameof(PosterPath));

    private Bitmap? _bitmap;
    private bool _isAttached;

    public string? PosterPath
    {
        get => GetValue(PosterPathProperty);
        set => SetValue(PosterPathProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PosterPathProperty && _isAttached)
            LoadPoster();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        LoadPoster();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        base.OnDetachedFromVisualTree(e);
        ReleaseBitmap();
    }

    private void LoadPoster()
    {
        ReleaseBitmap();
        if (string.IsNullOrWhiteSpace(PosterPath) || !File.Exists(PosterPath))
            return;

        try
        {
            _bitmap = new Bitmap(PosterPath);
            Source = _bitmap;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Source = null;
        }
    }

    private void ReleaseBitmap()
    {
        var bitmap = _bitmap;
        _bitmap = null;
        Source = null;
        if (bitmap is not null)
            Dispatcher.UIThread.Post(bitmap.Dispose, DispatcherPriority.Background);
    }
}
