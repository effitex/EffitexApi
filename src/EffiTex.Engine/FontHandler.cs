using System.Globalization;
using System.Text;
using EffiTex.Core.Models;
using iText.Kernel.Pdf;

namespace EffiTex.Engine;

public class FontHandler
{
    public void Apply(PdfDocument pdf, List<FontOperation> operations)
    {
        if (operations == null) return;

        foreach (var op in operations)
        {
            switch (op.Op)
            {
                case "write_cidset":
                    writeCidset(pdf, op);
                    break;
                case "write_charset":
                    writeCharset(pdf, op);
                    break;
                case "set_encoding":
                    setEncoding(pdf, op);
                    break;
                case "set_differences":
                    setDifferences(pdf, op);
                    break;
                case "write_tounicode":
                    writeToUnicode(pdf, op);
                    break;
                case "set_widths":
                    setWidths(pdf, op);
                    break;
                case "add_font_descriptor":
                    addFontDescriptor(pdf, op);
                    break;
            }
        }
    }

    private static PdfDictionary lookupFont(PdfDocument pdf, int pageNumber, string fontName)
    {
        if (pageNumber < 1 || pageNumber > pdf.GetNumberOfPages())
        {
            throw new InvalidOperationException(
                $"Page {pageNumber} does not exist in document with {pdf.GetNumberOfPages()} pages.");
        }

        var page = pdf.GetPage(pageNumber);
        var resources = page.GetResources();
        var fontDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);

        if (fontDict == null)
        {
            throw new InvalidOperationException(
                $"Font \"{fontName}\" not found on page {pageNumber}. The page has no font resources.");
        }

        var font = fontDict.GetAsDictionary(new PdfName(fontName));
        if (font == null)
        {
            throw new InvalidOperationException(
                $"Font \"{fontName}\" not found on page {pageNumber}.");
        }

        return font;
    }

    private static PdfDictionary getOrCreateFontDescriptor(PdfDictionary fontDict, PdfDocument pdf)
    {
        var descriptor = fontDict.GetAsDictionary(new PdfName("FontDescriptor"));
        if (descriptor != null) return descriptor;

        // For CID fonts, look inside DescendantFonts
        var descendants = fontDict.GetAsArray(new PdfName("DescendantFonts"));
        if (descendants != null && descendants.Size() > 0)
        {
            var cidFont = descendants.GetAsDictionary(0);
            if (cidFont != null)
            {
                descriptor = cidFont.GetAsDictionary(new PdfName("FontDescriptor"));
                if (descriptor != null) return descriptor;
            }
        }

        return null;
    }

    private static void writeCidset(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);
        var descriptor = getOrCreateFontDescriptor(fontDict, pdf);

        if (descriptor == null)
        {
            throw new InvalidOperationException(
                $"Font \"{op.Font}\" on page {op.Page} has no FontDescriptor for CIDSet.");
        }

        int maxCid = 0;
        foreach (var cid in op.Cids)
        {
            if (cid > maxCid) maxCid = cid;
        }

        int byteCount = (maxCid / 8) + 1;
        var bytes = new byte[byteCount];

        foreach (var cid in op.Cids)
        {
            int byteIndex = cid / 8;
            int bitIndex = 7 - (cid % 8);
            bytes[byteIndex] |= (byte)(1 << bitIndex);
        }

        var stream = new PdfStream(bytes);
        stream.MakeIndirect(pdf);
        descriptor.Put(new PdfName("CIDSet"), stream);
    }

    private static void writeCharset(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);
        var descriptor = getOrCreateFontDescriptor(fontDict, pdf);

        if (descriptor == null)
        {
            throw new InvalidOperationException(
                $"Font \"{op.Font}\" on page {op.Page} has no FontDescriptor for CharSet.");
        }

        var sb = new StringBuilder();
        foreach (var name in op.GlyphNames)
        {
            sb.Append('/');
            sb.Append(name);
        }

        descriptor.Put(new PdfName("CharSet"), new PdfString(sb.ToString()));
    }

    private static void setEncoding(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);
        fontDict.Put(PdfName.Encoding, new PdfName(op.Encoding));
    }

    private static void setDifferences(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);

        var encodingDict = fontDict.GetAsDictionary(PdfName.Encoding);
        if (encodingDict == null)
        {
            encodingDict = new PdfDictionary();
            encodingDict.Put(PdfName.Type, PdfName.Encoding);
            fontDict.Put(PdfName.Encoding, encodingDict);
        }

        var differencesArray = new PdfArray();
        var sorted = new SortedDictionary<int, string>(op.Differences);

        foreach (var kvp in sorted)
        {
            differencesArray.Add(new PdfNumber(kvp.Key));
            differencesArray.Add(new PdfName(kvp.Value));
        }

        encodingDict.Put(new PdfName("Differences"), differencesArray);
    }

    private static void writeToUnicode(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);

        var sb = new StringBuilder();
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo");
        sb.AppendLine("<< /Registry (Adobe)");
        sb.AppendLine("/Ordering (UCS)");
        sb.AppendLine("/Supplement 0");
        sb.AppendLine(">> def");
        sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");

        var sorted = new SortedDictionary<int, string>(op.Mappings);
        int count = sorted.Count;
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} beginbfchar", count));

        foreach (var kvp in sorted)
        {
            var codeHex = kvp.Key.ToString("X4");
            var unicodeHex = new StringBuilder();
            foreach (var ch in kvp.Value)
            {
                unicodeHex.Append(((int)ch).ToString("X4"));
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "<{0}> <{1}>", codeHex, unicodeHex));
        }

        sb.AppendLine("endbfchar");
        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");

        var cmapBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var stream = new PdfStream(cmapBytes);
        stream.MakeIndirect(pdf);
        fontDict.Put(new PdfName("ToUnicode"), stream);
    }

    private static void setWidths(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);

        // Check for CID font (Type0 with DescendantFonts containing /W array)
        var descendants = fontDict.GetAsArray(new PdfName("DescendantFonts"));
        if (descendants != null && descendants.Size() > 0)
        {
            var cidFont = descendants.GetAsDictionary(0);
            if (cidFont != null)
            {
                setCidWidths(cidFont, op.Widths, pdf);
                return;
            }
        }

        // Simple font: update /Widths array
        var widthsArray = fontDict.GetAsArray(PdfName.Widths);
        var firstChar = fontDict.GetAsNumber(new PdfName("FirstChar"));
        int firstCharVal = firstChar?.IntValue() ?? 0;

        if (widthsArray == null)
        {
            // Determine range needed
            int minCode = int.MaxValue;
            int maxCode = int.MinValue;
            foreach (var kvp in op.Widths)
            {
                if (kvp.Key < minCode) minCode = kvp.Key;
                if (kvp.Key > maxCode) maxCode = kvp.Key;
            }

            firstCharVal = minCode;
            int lastCharVal = maxCode;
            int size = lastCharVal - firstCharVal + 1;

            widthsArray = new PdfArray();
            for (int i = 0; i < size; i++)
            {
                widthsArray.Add(new PdfNumber(0));
            }

            fontDict.Put(new PdfName("FirstChar"), new PdfNumber(firstCharVal));
            fontDict.Put(new PdfName("LastChar"), new PdfNumber(lastCharVal));
            fontDict.Put(PdfName.Widths, widthsArray);
        }

        foreach (var kvp in op.Widths)
        {
            int index = kvp.Key - firstCharVal;
            if (index >= 0 && index < widthsArray.Size())
            {
                widthsArray.Set(index, new PdfNumber(kvp.Value));
            }
        }
    }

    private static void setCidWidths(PdfDictionary cidFont, Dictionary<int, float> widths, PdfDocument pdf)
    {
        // Build a /W array in the format: [cid1 [width1] cid2 [width2] ...]
        var wArray = new PdfArray();
        var sorted = new SortedDictionary<int, float>(widths);

        foreach (var kvp in sorted)
        {
            wArray.Add(new PdfNumber(kvp.Key));
            var widthArr = new PdfArray();
            widthArr.Add(new PdfNumber(kvp.Value));
            wArray.Add(widthArr);
        }

        cidFont.Put(new PdfName("W"), wArray);
    }

    private static void addFontDescriptor(PdfDocument pdf, FontOperation op)
    {
        var fontDict = lookupFont(pdf, op.Page, op.Font);

        var descriptor = new PdfDictionary();
        descriptor.Put(PdfName.Type, new PdfName("FontDescriptor"));

        // Use the font's BaseFont name if available, otherwise use the resource name
        var baseFont = fontDict.GetAsName(new PdfName("BaseFont"));
        var fontName = baseFont != null ? baseFont.GetValue() : op.Font;
        descriptor.Put(new PdfName("FontName"), new PdfName(fontName));

        descriptor.Put(new PdfName("Flags"), new PdfNumber(32));
        descriptor.Put(new PdfName("FontBBox"), new PdfArray(new float[] { 0, 0, 1000, 1000 }));
        descriptor.Put(new PdfName("ItalicAngle"), new PdfNumber(0));
        descriptor.Put(new PdfName("Ascent"), new PdfNumber(800));
        descriptor.Put(new PdfName("Descent"), new PdfNumber(-200));
        descriptor.Put(new PdfName("CapHeight"), new PdfNumber(700));
        descriptor.Put(new PdfName("StemV"), new PdfNumber(80));

        descriptor.MakeIndirect(pdf);
        fontDict.Put(new PdfName("FontDescriptor"), descriptor);
    }
}
