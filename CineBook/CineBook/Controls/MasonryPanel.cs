using System;
using Avalonia;
using Avalonia.Controls;

namespace CineBook.Controls;

public sealed class MasonryPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<MasonryPanel, int>(nameof(Columns), 4);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<MasonryPanel, double>(nameof(ColumnSpacing), 8);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<MasonryPanel, double>(nameof(RowSpacing), 8);

    static MasonryPanel()
    {
        AffectsMeasure<MasonryPanel>(ColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnCount = Math.Max(1, Columns);
        var isWidthFinite = double.IsFinite(availableSize.Width);
        var columnWidth = isWidthFinite
            ? GetColumnWidth(availableSize.Width, columnCount)
            : double.PositiveInfinity;
        var columnHeights = new double[columnCount];
        var hasItems = new bool[columnCount];
        var maxDesiredWidth = 0d;

        foreach (var child in Children)
        {
            var column = FindShortestColumn(columnHeights);
            child.Measure(new Size(columnWidth, double.PositiveInfinity));

            if (!isWidthFinite)
                maxDesiredWidth = Math.Max(maxDesiredWidth, child.DesiredSize.Width);

            var y = columnHeights[column] + (hasItems[column] ? RowSpacing : 0);
            columnHeights[column] = y + child.DesiredSize.Height;
            hasItems[column] = true;
        }

        var desiredHeight = 0d;
        foreach (var height in columnHeights)
            desiredHeight = Math.Max(desiredHeight, height);

        var desiredWidth = isWidthFinite
            ? availableSize.Width
            : maxDesiredWidth * columnCount + ColumnSpacing * (columnCount - 1);
        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnCount = Math.Max(1, Columns);
        var columnWidth = GetColumnWidth(finalSize.Width, columnCount);
        var columnHeights = new double[columnCount];
        var hasItems = new bool[columnCount];

        foreach (var child in Children)
        {
            var column = FindShortestColumn(columnHeights);
            var y = columnHeights[column] + (hasItems[column] ? RowSpacing : 0);
            var x = column * (columnWidth + ColumnSpacing);
            child.Arrange(new Rect(x, y, columnWidth, child.DesiredSize.Height));
            columnHeights[column] = y + child.DesiredSize.Height;
            hasItems[column] = true;
        }

        return finalSize;
    }

    private double GetColumnWidth(double width, int columnCount) =>
        Math.Max(0, (width - ColumnSpacing * (columnCount - 1)) / columnCount);

    private static int FindShortestColumn(double[] heights)
    {
        var shortest = 0;
        for (var column = 1; column < heights.Length; column++)
        {
            if (heights[column] < heights[shortest])
                shortest = column;
        }

        return shortest;
    }
}
