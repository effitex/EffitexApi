using EffiTex.Engine.Models.Inspect;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace EffiTex.Engine;

public class ColorPairExtractor : IEventListener
{
    private readonly List<FilledRect> _filledRects = new();
    private readonly List<(string Foreground, string Background)> _pairs = new();

    public void EventOccurred(IEventData data, EventType type)
    {
        if (type == EventType.RENDER_PATH)
            handlePath((PathRenderInfo)data);
        else if (type == EventType.RENDER_TEXT)
            handleText((TextRenderInfo)data);
    }

    public ICollection<EventType> GetSupportedEvents()
    {
        return new HashSet<EventType> { EventType.RENDER_TEXT, EventType.RENDER_PATH };
    }

    public void ProcessPage(PdfPage page)
    {
        try
        {
            var processor = new PdfCanvasProcessor(this);
            processor.ProcessPageContent(page);
        }
        catch
        {
            // leave pairs empty for this page on error
        }
    }

    public List<(string Foreground, string Background)> GetPairs() => new(_pairs);

    public static List<ColorPairInfo> Aggregate(IEnumerable<(string Foreground, string Background)> pairs)
    {
        return pairs
            .GroupBy(p => (p.Foreground, p.Background))
            .Select(g => new ColorPairInfo
            {
                Foreground = g.Key.Foreground,
                Background = g.Key.Background,
                OccurrenceCount = g.Count()
            })
            .OrderByDescending(p => p.OccurrenceCount)
            .ToList();
    }

    private void handlePath(PathRenderInfo pathInfo)
    {
        if ((pathInfo.GetOperation() & PathRenderInfo.FILL) == 0) return;

        var fillColor = pathInfo.GetGraphicsState().GetFillColor();
        var colorHex = ColorToHex(fillColor);

        var ctm = pathInfo.GetCtm();
        var localRect = computePathBounds(pathInfo.GetPath());
        if (localRect == null) return;

        var pageRect = transformRect(localRect, ctm);
        _filledRects.Add(new FilledRect(colorHex, pageRect));
    }

    private static Rectangle computePathBounds(iText.Kernel.Geom.Path path)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        bool hasPoints = false;

        foreach (var subpath in path.GetSubpaths())
        {
            foreach (var point in subpath.GetPiecewiseLinearApproximation())
            {
                float px = (float)point.GetX(), py = (float)point.GetY();
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
                hasPoints = true;
            }
        }

        if (!hasPoints) return null;
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    private void handleText(TextRenderInfo textInfo)
    {
        if (textInfo.GetTextRenderMode() == 3) return;

        var foreground = ColorToHex(textInfo.GetGraphicsState().GetFillColor());

        var baseline = textInfo.GetBaseline();
        var ascentLine = textInfo.GetAscentLine();

        float x = baseline.GetStartPoint().Get(0);
        float y = baseline.GetStartPoint().Get(1);
        float width = baseline.GetEndPoint().Get(0) - x;
        float height = ascentLine.GetStartPoint().Get(1) - y;

        if (width < 0) { x += width; width = -width; }
        if (height < 0) { y += height; height = -height; }

        float midX = x + width / 2f;
        float midY = y + height / 2f;

        var background = "FFFFFF";
        for (int i = _filledRects.Count - 1; i >= 0; i--)
        {
            var r = _filledRects[i].Rect;
            if (midX >= r.GetLeft() && midX <= r.GetRight() &&
                midY >= r.GetBottom() && midY <= r.GetTop())
            {
                background = _filledRects[i].Color;
                break;
            }
        }

        _pairs.Add((foreground, background));
    }

    private static Rectangle transformRect(Rectangle localRect, Matrix ctm)
    {
        float a = ctm.Get(Matrix.I11), b = ctm.Get(Matrix.I12);
        float c = ctm.Get(Matrix.I21), d = ctm.Get(Matrix.I22);
        float e = ctm.Get(Matrix.I31), f = ctm.Get(Matrix.I32);

        float lx = localRect.GetX(), ly = localRect.GetY();
        float rx = lx + localRect.GetWidth(), ty = ly + localRect.GetHeight();

        float[] xs = { a * lx + c * ly + e, a * rx + c * ly + e, a * lx + c * ty + e, a * rx + c * ty + e };
        float[] ys = { b * lx + d * ly + f, b * rx + d * ly + f, b * lx + d * ty + f, b * rx + d * ty + f };

        float minX = xs.Min();
        float maxX = xs.Max();
        float minY = ys.Min();
        float maxY = ys.Max();
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    private static string ColorToHex(Color color)
    {
        if (color == null) return "000000";

        var rgb = color.GetColorValue();

        float r, g, b;
        if (color is DeviceRgb || color is CalRgb)
        {
            r = rgb[0]; g = rgb[1]; b = rgb[2];
        }
        else if (color is DeviceGray || color is CalGray)
        {
            r = g = b = rgb[0];
        }
        else if (color is DeviceCmyk)
        {
            float cy = rgb[0], m = rgb[1], y = rgb[2], k = rgb[3];
            r = (1 - cy) * (1 - k);
            g = (1 - m) * (1 - k);
            b = (1 - y) * (1 - k);
        }
        else
        {
            return "000000";
        }

        int ri = (int)Math.Round(r * 255);
        int gi = (int)Math.Round(g * 255);
        int bi = (int)Math.Round(b * 255);
        return $"{ri:X2}{gi:X2}{bi:X2}";
    }

    private record FilledRect(string Color, Rectangle Rect);
}
