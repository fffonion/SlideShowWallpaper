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
        double[] columnPositions = BuildColumnPositions(rows, anchor.X, horizontalGap);
        double y = anchor.Y;
        foreach (List<HardwareOverlayElement> row in rows)
        {
            double rowHeight = 0;
            int column = 0;
            foreach (HardwareOverlayElement element in row.OrderBy(element => element.X).ThenBy(element => element.Y))
            {
                (element.X, element.Y) = QuantizePosition(
                    columnPositions[Math.Min(column, columnPositions.Length - 1)],
                    y,
                    double.MaxValue,
                    double.MaxValue);
                rowHeight = Math.Max(rowHeight, Math.Max(0, element.Height));
                column++;
            }

            y += rowHeight + verticalGap;
        }

        return true;
    }

    public static double QuantizeCoordinate(double value, double maxValue)
    {
        double maxWholePixel = double.IsFinite(maxValue) ? Math.Floor(Math.Max(0, maxValue)) : double.MaxValue;
        return Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, maxWholePixel);
    }

    public static (double X, double Y) QuantizePosition(double x, double y, double maxX, double maxY)
    {
        return (QuantizeCoordinate(x, maxX), QuantizeCoordinate(y, maxY));
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

    private static double[] BuildColumnPositions(IReadOnlyList<List<HardwareOverlayElement>> rows, double startX, double horizontalGap)
    {
        int columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        if (columnCount == 0)
        {
            return [];
        }

        List<HardwareOverlayElement> referenceRow = rows[0].OrderBy(element => element.X).ThenBy(element => element.Y).ToList();
        var positions = new double[columnCount];
        positions[0] = startX;
        for (int column = 1; column < columnCount; column++)
        {
            double previousWidth = column - 1 < referenceRow.Count
                ? Math.Max(0, referenceRow[column - 1].Width)
                : GetMaxColumnWidth(rows, column - 1);
            positions[column] = positions[column - 1] + previousWidth + horizontalGap;
        }

        return positions;
    }

    private static double GetMaxColumnWidth(IEnumerable<List<HardwareOverlayElement>> rows, int column)
    {
        return rows
            .Select(row => row.OrderBy(element => element.X).ThenBy(element => element.Y).ElementAtOrDefault(column))
            .Where(element => element is not null)
            .Max(element => Math.Max(0, element!.Width));
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
