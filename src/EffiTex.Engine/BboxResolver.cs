using EffiTex.Core.Models;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace EffiTex.Engine;

public class BboxResolver
{
    private const float TOLERANCE = 2f;

    public List<int> Resolve(PdfPage page, BoundingBox bbox)
    {
        var listener = new BboxEventListener();
        var processor = new PdfCanvasProcessor(listener);
        processor.ProcessPageContent(page);

        var targetRect = new Rectangle(
            bbox.X - TOLERANCE,
            bbox.Y - TOLERANCE,
            bbox.Width + 2 * TOLERANCE,
            bbox.Height + 2 * TOLERANCE);

        var result = new List<int>();
        for (int i = 0; i < listener.OperatorBounds.Count; i++)
        {
            if (intersects(targetRect, listener.OperatorBounds[i]))
            {
                result.Add(i);
            }
        }

        return result;
    }

    private static bool intersects(Rectangle a, Rectangle b)
    {
        float aLeft = a.GetX();
        float aBottom = a.GetY();
        float aRight = aLeft + a.GetWidth();
        float aTop = aBottom + a.GetHeight();

        float bLeft = b.GetX();
        float bBottom = b.GetY();
        float bRight = bLeft + b.GetWidth();
        float bTop = bBottom + b.GetHeight();

        return aLeft < bRight && aRight > bLeft && aBottom < bTop && aTop > bBottom;
    }

    private class BboxEventListener : IEventListener
    {
        public List<Rectangle> OperatorBounds { get; } = new();

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_TEXT)
            {
                var textInfo = (TextRenderInfo)data;
                var baseline = textInfo.GetBaseline();
                var ascentLine = textInfo.GetAscentLine();

                float x = baseline.GetStartPoint().Get(0);
                float y = baseline.GetStartPoint().Get(1);
                float width = baseline.GetEndPoint().Get(0) - x;
                float height = ascentLine.GetStartPoint().Get(1) - y;

                if (width < 0)
                {
                    x += width;
                    width = -width;
                }
                if (height < 0)
                {
                    y += height;
                    height = -height;
                }

                OperatorBounds.Add(new Rectangle(x, y, width, height));
            }
            else if (type == EventType.RENDER_IMAGE)
            {
                var imageInfo = (ImageRenderInfo)data;
                var matrix = imageInfo.GetImageCtm();

                float x = matrix.Get(Matrix.I31);
                float y = matrix.Get(Matrix.I32);
                float width = matrix.Get(Matrix.I11);
                float height = matrix.Get(Matrix.I22);

                if (width < 0)
                {
                    x += width;
                    width = -width;
                }
                if (height < 0)
                {
                    y += height;
                    height = -height;
                }

                OperatorBounds.Add(new Rectangle(x, y, width, height));
            }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new HashSet<EventType> { EventType.RENDER_TEXT, EventType.RENDER_IMAGE };
        }
    }
}
