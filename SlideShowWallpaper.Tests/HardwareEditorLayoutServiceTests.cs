using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using Windows.Foundation;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareEditorLayoutServiceTests
{
    [Fact]
    public void SelectIntersectingElements_WithSelectionRectangle_ReturnsIntersectingIds()
    {
        var elements = new[]
        {
            new HardwareOverlayElement { Id = "a", X = 10, Y = 10, Width = 30, Height = 20 },
            new HardwareOverlayElement { Id = "b", X = 60, Y = 10, Width = 30, Height = 20 },
            new HardwareOverlayElement { Id = "c", X = 10, Y = 60, Width = 30, Height = 20 },
        };

        IReadOnlyList<string> ids = HardwareEditorLayoutService.SelectIntersectingElementIds(
            elements,
            new Rect(0, 0, 95, 35));

        Assert.Equal(["a", "b"], ids);
    }

    [Fact]
    public void ApplyGridSpacing_UsesTopLeftRightAndDownNeighborsAsSpacingReference()
    {
        var elements = new List<HardwareOverlayElement>
        {
            new() { Id = "top-left", X = 10, Y = 10, Width = 20, Height = 10 },
            new() { Id = "top-right", X = 45, Y = 10, Width = 30, Height = 10 },
            new() { Id = "down-left", X = 10, Y = 35, Width = 20, Height = 12 },
            new() { Id = "down-right", X = 45, Y = 35, Width = 30, Height = 12 },
        };

        bool arranged = HardwareEditorLayoutService.ApplyGridSpacing(elements);

        Assert.True(arranged);
        Assert.Equal(10, elements[0].X);
        Assert.Equal(10, elements[0].Y);
        Assert.Equal(45, elements[1].X);
        Assert.Equal(10, elements[1].Y);
        Assert.Equal(10, elements[2].X);
        Assert.Equal(35, elements[2].Y);
        Assert.Equal(45, elements[3].X);
        Assert.Equal(35, elements[3].Y);
    }

    [Fact]
    public void ApplyGridSpacing_WithUnevenRows_AppliesReferenceGapsAcrossRows()
    {
        var elements = new List<HardwareOverlayElement>
        {
            new() { Id = "a", X = 10, Y = 10, Width = 20, Height = 10 },
            new() { Id = "b", X = 45, Y = 10, Width = 20, Height = 10 },
            new() { Id = "c", X = 90, Y = 11, Width = 20, Height = 10 },
            new() { Id = "d", X = 12, Y = 35, Width = 20, Height = 10 },
            new() { Id = "e", X = 62, Y = 36, Width = 20, Height = 10 },
        };

        bool arranged = HardwareEditorLayoutService.ApplyGridSpacing(elements);

        Assert.True(arranged);
        Assert.Equal(10, elements[0].X);
        Assert.Equal(10, elements[0].Y);
        Assert.Equal(45, elements[1].X);
        Assert.Equal(10, elements[1].Y);
        Assert.Equal(80, elements[2].X);
        Assert.Equal(10, elements[2].Y);
        Assert.Equal(10, elements[3].X);
        Assert.Equal(35, elements[3].Y);
        Assert.Equal(45, elements[4].X);
        Assert.Equal(35, elements[4].Y);
    }

    [Fact]
    public void ApplyGridSpacing_WithDifferentWidths_AlignsColumnsAcrossRows()
    {
        var elements = new List<HardwareOverlayElement>
        {
            new() { Id = "top-left", X = 10, Y = 10, Width = 20, Height = 10 },
            new() { Id = "top-right", X = 45, Y = 10, Width = 30, Height = 10 },
            new() { Id = "down-left-wide", X = 10, Y = 35, Width = 60, Height = 12 },
            new() { Id = "down-right", X = 120, Y = 35, Width = 30, Height = 12 },
        };

        bool arranged = HardwareEditorLayoutService.ApplyGridSpacing(elements);

        Assert.True(arranged);
        Assert.Equal(10, elements[0].X);
        Assert.Equal(45, elements[1].X);
        Assert.Equal(10, elements[2].X);
        Assert.Equal(45, elements[3].X);
    }

    [Fact]
    public void QuantizePosition_RoundsToWholePixelsAndClampsToBounds()
    {
        (double x, double y) = HardwareEditorLayoutService.QuantizePosition(
            x: 12.6,
            y: 3.2,
            maxX: 12.4,
            maxY: 8.7);

        Assert.Equal(12, x);
        Assert.Equal(3, y);
    }
}
