using System;
using Avalonia;
using Avalonia.Controls;

namespace CineBook.Controls;

public sealed class AspectRatioPanel : Panel
{
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<AspectRatioPanel, double>(nameof(AspectRatio), 1);

    static AspectRatioPanel()
    {
        AffectsMeasure<AspectRatioPanel>(AspectRatioProperty);
    }

    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ratio = double.IsFinite(AspectRatio) && AspectRatio > 0 ? AspectRatio : 1;
        var width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : double.IsFinite(availableSize.Height)
                ? availableSize.Height * ratio
                : 250;
        var height = width / ratio;

        if (double.IsFinite(availableSize.Height) && height > availableSize.Height)
        {
            height = Math.Max(0, availableSize.Height);
            width = height * ratio;
        }

        var childSize = new Size(width, height);
        foreach (var child in Children)
            child.Measure(childSize);

        return childSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = new Rect(finalSize);
        foreach (var child in Children)
            child.Arrange(bounds);

        return finalSize;
    }
}
