using System;
using Avalonia;
using Avalonia.Controls;

namespace CineBook.Controls;

public sealed class ResponsiveMetadataPanel : Panel
{
    private const double InlineSpacing = 0;
    private const double WrappedLineSpacing = 0;
    private bool _fitsOnOneLine;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            foreach (var child in Children)
                child.Measure(availableSize);
            return Children.Count == 0 ? new Size() : Children[0].DesiredSize;
        }

        var rating = Children[0];
        var genres = Children[1];
        rating.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        genres.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        _fitsOnOneLine = double.IsPositiveInfinity(availableSize.Width) ||
                         rating.DesiredSize.Width + InlineSpacing + genres.DesiredSize.Width <= availableSize.Width;

        if (_fitsOnOneLine)
        {
            var width = rating.DesiredSize.Width + InlineSpacing + genres.DesiredSize.Width;
            return new Size(width, Math.Max(rating.DesiredSize.Height, genres.DesiredSize.Height));
        }

        genres.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(
            availableSize.Width,
            rating.DesiredSize.Height + WrappedLineSpacing + genres.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            foreach (var child in Children)
                child.Arrange(new Rect(finalSize));
            return finalSize;
        }

        var rating = Children[0];
        var genres = Children[1];

        if (_fitsOnOneLine)
        {
            var lineHeight = Math.Max(rating.DesiredSize.Height, genres.DesiredSize.Height);
            rating.Arrange(new Rect(0, (lineHeight - rating.DesiredSize.Height) / 2,
                rating.DesiredSize.Width, rating.DesiredSize.Height));
            genres.Arrange(new Rect(Math.Max(0, finalSize.Width - genres.DesiredSize.Width),
                (lineHeight - genres.DesiredSize.Height) / 2,
                genres.DesiredSize.Width, genres.DesiredSize.Height));
        }
        else
        {
            rating.Arrange(new Rect(0, 0, rating.DesiredSize.Width, rating.DesiredSize.Height));
            genres.Arrange(new Rect(0, rating.DesiredSize.Height + WrappedLineSpacing,
                finalSize.Width, genres.DesiredSize.Height));
        }

        return finalSize;
    }
}
