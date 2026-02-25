using System.Text;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Tagging;
using iText.Kernel.Pdf.Xobject;

namespace EffiTex.Engine.Tests;

public static class FixtureGenerator
{
    private static readonly string _fixturesPath = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    public static string GetFixturePath(string filename)
    {
        return System.IO.Path.Combine(_fixturesPath, filename);
    }

    public static string EnsureUntaggedSimple()
    {
        var path = GetFixturePath("untagged_simple.pdf");
        if (File.Exists(path)) return path;

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        var page = pdf.AddNewPage(PageSize.LETTER);
        var canvas = new PdfCanvas(page);
        var font = PdfFontFactory.CreateFont("Helvetica");

        canvas.BeginText()
            .SetFontAndSize(font, 12)
            .MoveText(72, 700)
            .ShowText("Hello World")
            .EndText();

        canvas.BeginText()
            .SetFontAndSize(font, 24)
            .MoveText(72, 650)
            .ShowText("Large Heading Text")
            .EndText();

        pdf.Close();
        return path;
    }

    public static string EnsureAnnotated()
    {
        var path = GetFixturePath("annotated.pdf");
        if (File.Exists(path)) return path;

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        var page = pdf.AddNewPage(PageSize.LETTER);
        var canvas = new PdfCanvas(page);
        var font = PdfFontFactory.CreateFont("Helvetica");

        canvas.BeginText()
            .SetFontAndSize(font, 12)
            .MoveText(72, 700)
            .ShowText("Document with annotations")
            .EndText();

        var linkRect = new Rectangle(72, 500, 200, 14);
        var linkAnnot = new PdfLinkAnnotation(linkRect);
        linkAnnot.SetAction(PdfAction.CreateURI("https://example.com"));
        page.AddAnnotation(linkAnnot);

        var widgetDict = new PdfDictionary();
        widgetDict.Put(PdfName.Type, PdfName.Annot);
        widgetDict.Put(PdfName.Subtype, new PdfName("Widget"));
        widgetDict.Put(new PdfName("T"), new PdfString("first_name"));
        widgetDict.Put(new PdfName("FT"), new PdfName("Tx"));
        widgetDict.Put(new PdfName("TU"), new PdfString("First Name"));
        var widgetRect = new PdfArray(new float[] { 100, 300, 300, 320 });
        widgetDict.Put(PdfName.Rect, widgetRect);
        widgetDict.MakeIndirect(pdf);

        var annots = page.GetPdfObject().GetAsArray(PdfName.Annots);
        if (annots == null)
        {
            annots = new PdfArray();
            page.GetPdfObject().Put(PdfName.Annots, annots);
        }
        annots.Add(widgetDict);

        var acroForm = new PdfDictionary();
        var fields = new PdfArray();
        fields.Add(widgetDict);
        acroForm.Put(new PdfName("Fields"), fields);
        acroForm.MakeIndirect(pdf);
        pdf.GetCatalog().GetPdfObject().Put(new PdfName("AcroForm"), acroForm);

        pdf.Close();
        return path;
    }

    public static string EnsureStructuredHeadings()
    {
        var path = GetFixturePath("structured_headings.pdf");
        if (File.Exists(path)) return path;

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        pdf.SetTagged();
        var structRoot = pdf.GetStructTreeRoot();

        var docElem = new PdfStructElem(pdf, new PdfName("Document"));
        structRoot.AddKid(docElem);

        var page1 = pdf.AddNewPage(PageSize.LETTER);
        var canvas1 = new PdfCanvas(page1);
        var font = PdfFontFactory.CreateFont("Helvetica");

        var h1Elem = new PdfStructElem(pdf, new PdfName("H1"));
        docElem.AddKid(h1Elem);
        addTaggedText(pdf, page1, canvas1, font, 24, 72, 700, "Chapter One", h1Elem);

        var h2Elem = new PdfStructElem(pdf, new PdfName("H2"));
        docElem.AddKid(h2Elem);
        addTaggedText(pdf, page1, canvas1, font, 18, 72, 660, "Section 1.1", h2Elem);

        var pElem = new PdfStructElem(pdf, new PdfName("P"));
        docElem.AddKid(pElem);
        addTaggedText(pdf, page1, canvas1, font, 12, 72, 630, "Body text paragraph.", pElem);

        var page2 = pdf.AddNewPage(PageSize.LETTER);
        var canvas2 = new PdfCanvas(page2);

        var h3Elem = new PdfStructElem(pdf, new PdfName("H3"));
        docElem.AddKid(h3Elem);
        addTaggedText(pdf, page2, canvas2, font, 14, 72, 700, "Subsection 1.1.1", h3Elem);

        pdf.Close();
        return path;
    }

    public static string EnsureScanned()
    {
        var path = GetFixturePath("scanned.pdf");
        if (File.Exists(path)) return path;

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        var page = pdf.AddNewPage(PageSize.LETTER);
        var canvas = new PdfCanvas(page);

        var imgBytes = createMinimalGrayImage(200, 100);
        var imgData = iText.IO.Image.ImageDataFactory.Create(imgBytes);
        var imgXobj = new PdfImageXObject(imgData);
        canvas.AddXObjectAt(imgXobj, 72, 600);

        pdf.Close();
        return path;
    }

    public static string EnsureMixedFonts()
    {
        var path = GetFixturePath("mixed_fonts.pdf");
        if (File.Exists(path)) return path;

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);

        // Page 1: CIDFontType2 (embedded TrueType) + standard Type1
        var page1 = pdf.AddNewPage(PageSize.LETTER);
        var canvas1 = new PdfCanvas(page1);

        // CIDFontType2: embed a system TrueType font with Identity-H encoding
        var arialPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        if (!File.Exists(arialPath))
            arialPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");

        var cidFont = PdfFontFactory.CreateFont(arialPath, PdfEncodings.IDENTITY_H,
            PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        canvas1.BeginText()
            .SetFontAndSize(cidFont, 14)
            .MoveText(72, 700)
            .ShowText("CID Font Text ABC")
            .EndText();

        // Standard Type1 font (not embedded)
        var type1Font = PdfFontFactory.CreateFont("Helvetica");
        canvas1.BeginText()
            .SetFontAndSize(type1Font, 12)
            .MoveText(72, 660)
            .ShowText("Type1 Text")
            .EndText();

        // Page 2: Type3 font (manually constructed)
        var page2 = pdf.AddNewPage(PageSize.LETTER);
        addType3Font(pdf, page2);

        pdf.Close();
        return path;
    }

    private static void addType3Font(PdfDocument pdf, PdfPage page)
    {
        // Build a Type3 font with custom glyph names (no AGL mapping → unmappable)
        var fontDict = new PdfDictionary();
        fontDict.Put(PdfName.Type, PdfName.Font);
        fontDict.Put(PdfName.Subtype, new PdfName("Type3"));
        fontDict.Put(new PdfName("FontBBox"), new PdfArray(new int[] { 0, 0, 1000, 800 }));
        fontDict.Put(new PdfName("FontMatrix"), new PdfArray(new float[] { 0.001f, 0, 0, 0.001f, 0, 0 }));
        fontDict.Put(PdfName.FirstChar, new PdfNumber(65)); // 'A'
        fontDict.Put(PdfName.LastChar, new PdfNumber(67));   // 'C'
        fontDict.Put(PdfName.Widths, new PdfArray(new int[] { 600, 500, 550 }));

        // Encoding with Differences using non-AGL glyph names
        var encodingDict = new PdfDictionary();
        var differences = new PdfArray();
        differences.Add(new PdfNumber(65));
        differences.Add(new PdfName("glyph0"));
        differences.Add(new PdfName("glyph1"));
        differences.Add(new PdfName("glyph2"));
        encodingDict.Put(new PdfName("Differences"), differences);
        fontDict.Put(PdfName.Encoding, encodingDict);

        // CharProcs — glyph drawing procedures
        var charProcs = new PdfDictionary();

        var streamA = new PdfStream(Encoding.ASCII.GetBytes(
            "600 0 0 0 600 800 d1\n100 100 400 600 re f\n"));
        streamA.MakeIndirect(pdf);
        charProcs.Put(new PdfName("glyph0"), streamA);

        var streamB = new PdfStream(Encoding.ASCII.GetBytes(
            "500 0 0 0 500 800 d1\n50 400 m 50 600 250 750 450 600 c 450 200 250 50 50 200 c h f\n"));
        streamB.MakeIndirect(pdf);
        charProcs.Put(new PdfName("glyph1"), streamB);

        var streamC = new PdfStream(Encoding.ASCII.GetBytes(
            "550 0 0 0 550 800 d1\n275 750 m 50 50 l 500 50 l h f\n"));
        streamC.MakeIndirect(pdf);
        charProcs.Put(new PdfName("glyph2"), streamC);

        fontDict.Put(new PdfName("CharProcs"), charProcs);
        fontDict.MakeIndirect(pdf);

        // Register font in page resources
        var pageDict = page.GetPdfObject();
        var resources = pageDict.GetAsDictionary(PdfName.Resources);
        if (resources == null)
        {
            resources = new PdfDictionary();
            pageDict.Put(PdfName.Resources, resources);
        }
        var fontsRes = resources.GetAsDictionary(PdfName.Font);
        if (fontsRes == null)
        {
            fontsRes = new PdfDictionary();
            resources.Put(PdfName.Font, fontsRes);
        }
        fontsRes.Put(new PdfName("T3F1"), fontDict);

        // Write content stream using the Type3 font
        var contentBytes = Encoding.ASCII.GetBytes(
            "BT\n/T3F1 24 Tf\n72 700 Td\n(ABC) Tj\nET\n");
        var contentStream = new PdfStream(contentBytes);
        contentStream.MakeIndirect(pdf);
        pageDict.Put(PdfName.Contents, contentStream);
    }

    public static string CreateTempPdf()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"effitex_test_{Guid.NewGuid():N}.pdf");
        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        var page = pdf.AddNewPage(PageSize.LETTER);
        var canvas = new PdfCanvas(page);
        var font = PdfFontFactory.CreateFont("Helvetica");
        canvas.BeginText().SetFontAndSize(font, 12).MoveText(72, 700).ShowText("Temp").EndText();
        pdf.Close();
        return path;
    }

    private static void addTaggedText(PdfDocument pdf, PdfPage page, PdfCanvas canvas,
        PdfFont font, float fontSize, float x, float y, string text, PdfStructElem elem)
    {
        var mcr = new PdfMcrNumber(page, elem);
        int mcid = mcr.GetMcid();

        canvas.OpenTag(new CanvasTag(new PdfName(elem.GetRole().GetValue()), mcid));
        canvas.BeginText()
            .SetFontAndSize(font, fontSize)
            .MoveText(x, y)
            .ShowText(text)
            .EndText();
        canvas.CloseTag();
    }

    private static byte[] createMinimalGrayImage(int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(new byte[] { 0x42, 0x4D });
        int rowSize = ((width * 8 + 31) / 32) * 4;
        int imageSize = rowSize * height;
        int fileSize = 54 + 1024 + imageSize;
        bw.Write(fileSize);
        bw.Write(0);
        bw.Write(54 + 1024);

        bw.Write(40);
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1);
        bw.Write((short)8);
        bw.Write(0);
        bw.Write(imageSize);
        bw.Write(2835);
        bw.Write(2835);
        bw.Write(256);
        bw.Write(0);

        for (int i = 0; i < 256; i++)
        {
            bw.Write((byte)i);
            bw.Write((byte)i);
            bw.Write((byte)i);
            bw.Write((byte)0);
        }

        var rng = new Random(42);
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
                bw.Write((byte)rng.Next(180, 240));
            for (int pad = width; pad < rowSize; pad++)
                bw.Write((byte)0);
        }

        return ms.ToArray();
    }
}
