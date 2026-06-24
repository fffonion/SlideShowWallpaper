using SlideShowWallpaper.Models;
using Windows.Foundation;

namespace SlideShowWallpaper.Services;

public static class HardwareEditorLayoutService
{
    private const double RowTolerance = 12;

    public static IReadOnlyList<string> SelectIntersectingElementIds(IEnumerable<HardwareOverlayElement> elements, Rect selection)
    {
        return elements
            .Where(element => Intersects(selection, CreateRect(element)))
            .Select(element => element.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    public static bool ApplyGridSpacing(IList<HardwareOverlayElement> elements)
    {
        if (elements.Count < 3)
        {
            return false;
        }

        HardwareOverlayElement anchor = elements
            .OrderBy(element => element.Y)
            .ThenBy(element => element.X)
            .First();
        HardwareOverlayElement? right = elements
            .Where(element => !ReferenceEquals(element, anchor) && element.X > anchor.X)
            .OrderBy(element => Math.Abs(element.Y - anchor.Y))
            .ThenBy(element => element.X)
            .FirstOrDefault();
        HardwareOverlayElement? down = elements
            .Where(element => !ReferenceEquals(element, anchor) && element.Y > anchor.Y)
            .OrderBy(element => Math.Abs(element.X - anchor.X))
            .ThenBy(element => element.Y)
            .FirstOrDefault();
        if (right is null || down is null)
        {
            return false;
        }

        double horizontalGap = Math.Max(0, right.X - (anchor.X + anchor.Width));
        double verticalGap = Math.Max(0, down.Y - (anchor.Y + anchor.Height));
        List<List<HardwareOverlayElement>> rows = GroupRows(elements);
        double y = anchor.Y;
        foreach (List<HardwareOverlayElement> row in rows)
        {
            double x = anchor.X;
            double rowHeight = 0;
            foreach (HardwareOverlayElement element in row.OrderBy(element => element.X).ThenBy(element => element.Y))
            {
                element.X = x;
                element.Y = y;
                x += Math.Max(0, element.Width) + horizontalGap;
                rowHeight = Math.Max(rowHeight, Math.Max(0, element.Height));
            }

            y += rowHeight + verticalGap;
        }

        return true;
    }

    private static List<List<HardwareOverlayElement>> GroupRows(IEnumerable<HardwareOverlayElement> elements)
    {
        var rows = new List<List<HardwareOverlayElement>>();
        foreach (HardwareOverlayElement element in elements.OrderBy(element => element.Y).ThenBy(element => element.X))
        {
            List<HardwareOverlayElement>? row = rows.FirstOrDefault(items => Math.Abs(items[0].Y - element.Y) <= RowTolerance);
            if (row is null)
            {
                rows.Add([element]);
            }
            else
            {
                row.Add(element);
            }
        }

        return rows;
    }

    private static Rect CreateRect(HardwareOverlayElement element)
    {
        return new Rect(element.X, element.Y, Math.Max(0, element.Width), Math.Max(0, element.Height));
    }

    private static bool Intersects(Rect left, Rect right)
    {
        return left.X < right.X + right.Width
            && left.X + left.Width > right.X
            && left.Y < right.Y + right.Height
            && left.Y + left.Height > right.Y;
    }
}
