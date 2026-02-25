using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class OcrHandlerTests
{
    [Fact]
    public void Apply_TwoWords_TextOperatorsExistInStream()
    {
        var source = FixtureGenerator.EnsureScanned();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new OcrHandler();
                handler.Apply(pdf, new List<OcrPage>
                {
                    new()
                    {
                        Page = 1,
                        Words = new List<OcrWord>
                        {
                            new() { Text = "HELLO", Bbox = new BoundingBox { X = 72, Y = 720, Width = 60, Height = 14 } },
                            new() { Text = "WORLD", Bbox = new BoundingBox { X = 140, Y = 720, Width = 60, Height = 14 } }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var bytes = page.GetContentBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(bytes);
                content.Should().Contain("BT");
                content.Should().Contain("ET");
                content.Should().Contain("Tf");
                content.Should().Contain("Tm");
                content.Should().Contain("Tj");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_TwoWords_TextRenderingModeIsInvisible()
    {
        var source = FixtureGenerator.EnsureScanned();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new OcrHandler();
                handler.Apply(pdf, new List<OcrPage>
                {
                    new()
                    {
                        Page = 1,
                        Words = new List<OcrWord>
                        {
                            new() { Text = "HELLO", Bbox = new BoundingBox { X = 72, Y = 720, Width = 60, Height = 14 } },
                            new() { Text = "WORLD", Bbox = new BoundingBox { X = 140, Y = 720, Width = 60, Height = 14 } }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var bytes = page.GetContentBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(bytes);
                content.Should().Contain("3 Tr");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_TwoWords_TextContentMatchesWordStrings()
    {
        var source = FixtureGenerator.EnsureScanned();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new OcrHandler();
                handler.Apply(pdf, new List<OcrPage>
                {
                    new()
                    {
                        Page = 1,
                        Words = new List<OcrWord>
                        {
                            new() { Text = "HELLO", Bbox = new BoundingBox { X = 72, Y = 720, Width = 60, Height = 14 } },
                            new() { Text = "WORLD", Bbox = new BoundingBox { X = 140, Y = 720, Width = 60, Height = 14 } }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var bytes = page.GetContentBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(bytes);
                content.Should().Contain("(HELLO)");
                content.Should().Contain("(WORLD)");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_MultiplePages_WordsGoToCorrectPages()
    {
        // Create a multi-page scanned PDF
        var multiPageSource = Path.GetTempFileName() + ".pdf";
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            // Create a 2-page image-only PDF
            using (var writer = new PdfWriter(multiPageSource))
            using (var pdf = new PdfDocument(writer))
            {
                var page1 = pdf.AddNewPage(iText.Kernel.Geom.PageSize.LETTER);
                var canvas1 = new PdfCanvas(page1);
                var imgBytes = CreateMinimalImage(100, 50);
                var imgData = iText.IO.Image.ImageDataFactory.Create(imgBytes);
                var imgXobj = new PdfImageXObject(imgData);
                canvas1.AddXObjectAt(imgXobj, 72, 600);

                var page2 = pdf.AddNewPage(iText.Kernel.Geom.PageSize.LETTER);
                var canvas2 = new PdfCanvas(page2);
                canvas2.AddXObjectAt(imgXobj, 72, 600);
            }

            using (var reader = new PdfReader(multiPageSource))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new OcrHandler();
                handler.Apply(pdf, new List<OcrPage>
                {
                    new()
                    {
                        Page = 1,
                        Words = new List<OcrWord>
                        {
                            new() { Text = "PAGE1WORD", Bbox = new BoundingBox { X = 72, Y = 720, Width = 80, Height = 14 } }
                        }
                    },
                    new()
                    {
                        Page = 2,
                        Words = new List<OcrWord>
                        {
                            new() { Text = "PAGE2WORD", Bbox = new BoundingBox { X = 72, Y = 720, Width = 80, Height = 14 } }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page1Bytes = pdf.GetPage(1).GetContentBytes();
                var page1Content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(page1Bytes);
                page1Content.Should().Contain("(PAGE1WORD)");
                page1Content.Should().NotContain("(PAGE2WORD)");

                var page2Bytes = pdf.GetPage(2).GetContentBytes();
                var page2Content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(page2Bytes);
                page2Content.Should().Contain("(PAGE2WORD)");
                page2Content.Should().NotContain("(PAGE1WORD)");
            }
        }
        finally
        {
            File.Delete(multiPageSource);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_NullPages_IsNoOp()
    {
        var source = FixtureGenerator.EnsureScanned();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            // Get original content bytes for comparison
            byte[] originalContentBytes;
            using (var reader = new PdfReader(source))
            using (var pdf = new PdfDocument(reader))
            {
                originalContentBytes = pdf.GetPage(1).GetContentBytes();
            }

            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new OcrHandler();
                handler.Apply(pdf, null);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var bytes = page.GetContentBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(bytes);
                // Should not contain any OCR text operators
                content.Should().NotContain("3 Tr");
            }
        }
        finally { File.Delete(temp); }
    }

    /// <summary>
    /// Creates a minimal BMP image for testing multi-page PDFs.
    /// </summary>
    private static byte[] CreateMinimalImage(int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(new byte[] { 0x42, 0x4D }); // BM header
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
