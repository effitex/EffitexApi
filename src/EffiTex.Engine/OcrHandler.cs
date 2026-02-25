using EffiTex.Core.Models;
using iText.Kernel.Font;
using iText.Kernel.Pdf;

namespace EffiTex.Engine;

public class OcrHandler
{
    public void Apply(PdfDocument pdf, List<OcrPage> pages)
    {
        if (pages == null || pages.Count == 0) return;

        foreach (var ocrPage in pages)
        {
            if (ocrPage.Words == null || ocrPage.Words.Count == 0) continue;

            var page = pdf.GetPage(ocrPage.Page);
            var font = PdfFontFactory.CreateFont("Helvetica");

            // Register font in page resources
            var resources = page.GetResources();
            var fontDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontDict == null)
            {
                fontDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontDict);
            }
            var fontObj = font.GetPdfObject();
            if (fontObj.GetIndirectReference() == null)
                fontObj.MakeIndirect(pdf);

            // Find or create font key
            string fontKey = null;
            foreach (var key in fontDict.KeySet())
            {
                var existing = fontDict.Get(key);
                if (existing?.GetIndirectReference()?.GetObjNumber() == fontObj.GetIndirectReference()?.GetObjNumber())
                {
                    fontKey = key.GetValue();
                    break;
                }
            }
            if (fontKey == null)
            {
                int idx = 1;
                while (fontDict.ContainsKey(new PdfName($"F{idx}"))) idx++;
                fontKey = $"F{idx}";
                fontDict.Put(new PdfName(fontKey), fontObj);
            }

            // Build content stream
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BT");
            sb.AppendLine("3 Tr"); // invisible text rendering mode

            foreach (var word in ocrPage.Words)
            {
                if (string.IsNullOrWhiteSpace(word.Text)) continue;
                if (word.Bbox == null) continue;

                float fontSize = word.Bbox.Height;
                if (fontSize <= 0) fontSize = 12f;

                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "/{0} {1:F2} Tf", fontKey, fontSize));
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "1 0 0 1 {0:F2} {1:F2} Tm", word.Bbox.X, word.Bbox.Y));
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "({0}) Tj", EscapePdfString(word.Text)));
            }

            sb.AppendLine("ET");

            // Append as new content stream after existing content
            var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(sb.ToString());
            var stream = page.NewContentStreamAfter();
            stream.SetData(bytes);
        }
    }

    private static string EscapePdfString(string text)
    {
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
