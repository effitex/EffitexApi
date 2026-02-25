using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class FontHandlerTests
{
    [Fact]
    public void Apply_WriteCidset_CreatesCidSetStreamInFontDescriptor()
    {
        var source = createPdfWithCidFont();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "write_cidset",
                        Font = "F1",
                        Page = 1,
                        Cids = new List<int> { 0, 1, 5, 10 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                // Navigate to CID font's FontDescriptor
                var descendants = fontDict.GetAsArray(new PdfName("DescendantFonts"));
                var cidFont = descendants.GetAsDictionary(0);
                var descriptor = cidFont.GetAsDictionary(new PdfName("FontDescriptor"));
                descriptor.Should().NotBeNull();

                var cidSetStream = descriptor.GetAsStream(new PdfName("CIDSet"));
                cidSetStream.Should().NotBeNull();

                var bytes = cidSetStream.GetBytes();
                bytes.Should().NotBeNull();
                bytes.Length.Should().BeGreaterThan(0);

                // Verify bit 0 is set (CID 0)
                (bytes[0] & 0x80).Should().NotBe(0, "CID 0 should be set");
                // Verify bit 1 is set (CID 1)
                (bytes[0] & 0x40).Should().NotBe(0, "CID 1 should be set");
                // Verify bit 5 is set (CID 5)
                (bytes[0] & 0x04).Should().NotBe(0, "CID 5 should be set");
                // Verify CID 10 is set (byte 1, bit 2)
                (bytes[1] & 0x20).Should().NotBe(0, "CID 10 should be set");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_WriteCharset_SetsCharSetInFontDescriptor()
    {
        var source = createPdfWithType1Font();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "write_charset",
                        Font = "F1",
                        Page = 1,
                        GlyphNames = new List<string> { "A", "B", "space" }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var descriptor = fontDict.GetAsDictionary(new PdfName("FontDescriptor"));
                descriptor.Should().NotBeNull();

                var charSet = descriptor.GetAsString(new PdfName("CharSet"));
                charSet.Should().NotBeNull();
                charSet.GetValue().Should().Be("/A/B/space");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_SetEncoding_SetsEncodingOnFontDictionary()
    {
        var source = createPdfWithSimpleFont();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "set_encoding",
                        Font = "F1",
                        Page = 1,
                        Encoding = "WinAnsiEncoding"
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var encoding = fontDict.GetAsName(PdfName.Encoding);
                encoding.Should().NotBeNull();
                encoding.GetValue().Should().Be("WinAnsiEncoding");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_SetDifferences_SetsDifferencesArrayInEncoding()
    {
        var source = createPdfWithEncodingDict();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "set_differences",
                        Font = "F1",
                        Page = 1,
                        Differences = new Dictionary<int, string>
                        {
                            { 65, "A" },
                            { 66, "B" },
                            { 120, "x" }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var encodingDict = fontDict.GetAsDictionary(PdfName.Encoding);
                encodingDict.Should().NotBeNull();

                var differences = encodingDict.GetAsArray(new PdfName("Differences"));
                differences.Should().NotBeNull();
                differences.Size().Should().Be(6); // 3 pairs: code name code name code name

                // Verify sorted order: 65 /A 66 /B 120 /x
                differences.GetAsNumber(0).IntValue().Should().Be(65);
                differences.GetAsName(1).GetValue().Should().Be("A");
                differences.GetAsNumber(2).IntValue().Should().Be(66);
                differences.GetAsName(3).GetValue().Should().Be("B");
                differences.GetAsNumber(4).IntValue().Should().Be(120);
                differences.GetAsName(5).GetValue().Should().Be("x");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_WriteToUnicode_CreatesToUnicodeStream()
    {
        var source = createPdfWithSimpleFont();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "write_tounicode",
                        Font = "F1",
                        Page = 1,
                        Mappings = new Dictionary<int, string>
                        {
                            { 0x0041, "A" },
                            { 0x0042, "B" }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var toUnicode = fontDict.GetAsStream(new PdfName("ToUnicode"));
                toUnicode.Should().NotBeNull();

                var cmapContent = System.Text.Encoding.ASCII.GetString(toUnicode.GetBytes());
                cmapContent.Should().Contain("beginbfchar");
                cmapContent.Should().Contain("endbfchar");
                cmapContent.Should().Contain("<0041>");
                cmapContent.Should().Contain("<0042>");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_SetWidths_UpdatesWidthEntries()
    {
        var source = createPdfWithWidths();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "set_widths",
                        Font = "F1",
                        Page = 1,
                        Widths = new Dictionary<int, float>
                        {
                            { 32, 250f },
                            { 33, 333f }
                        }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var widths = fontDict.GetAsArray(PdfName.Widths);
                widths.Should().NotBeNull();

                var firstChar = fontDict.GetAsNumber(new PdfName("FirstChar")).IntValue();
                int idx32 = 32 - firstChar;
                int idx33 = 33 - firstChar;

                widths.GetAsNumber(idx32).FloatValue().Should().Be(250f);
                widths.GetAsNumber(idx33).FloatValue().Should().Be(333f);
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_AddFontDescriptor_CreatesMinimalDescriptor()
    {
        var source = createPdfWithBareFont();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new FontHandler();
                handler.Apply(pdf, new List<FontOperation>
                {
                    new()
                    {
                        Op = "add_font_descriptor",
                        Font = "F1",
                        Page = 1
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var fontDict = page.GetResources().GetPdfObject()
                    .GetAsDictionary(PdfName.Font)
                    .GetAsDictionary(new PdfName("F1"));

                var descriptor = fontDict.GetAsDictionary(new PdfName("FontDescriptor"));
                descriptor.Should().NotBeNull();
                descriptor.GetAsName(PdfName.Type).GetValue().Should().Be("FontDescriptor");
                descriptor.GetAsName(new PdfName("FontName")).Should().NotBeNull();
                descriptor.GetAsNumber(new PdfName("Flags")).IntValue().Should().Be(32);
                descriptor.GetAsNumber(new PdfName("ItalicAngle")).IntValue().Should().Be(0);
                descriptor.GetAsNumber(new PdfName("Ascent")).IntValue().Should().Be(800);
                descriptor.GetAsNumber(new PdfName("Descent")).IntValue().Should().Be(-200);
                descriptor.GetAsNumber(new PdfName("CapHeight")).IntValue().Should().Be(700);
                descriptor.GetAsNumber(new PdfName("StemV")).IntValue().Should().Be(80);

                var bbox = descriptor.GetAsArray(new PdfName("FontBBox"));
                bbox.Should().NotBeNull();
                bbox.Size().Should().Be(4);
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_NonExistentFont_ThrowsInvalidOperationException()
    {
        var source = createPdfWithSimpleFont();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using var reader = new PdfReader(source);
            using var writer = new PdfWriter(temp);
            using var pdf = new PdfDocument(reader, writer);

            var handler = new FontHandler();
            var act = () => handler.Apply(pdf, new List<FontOperation>
            {
                new()
                {
                    Op = "set_encoding",
                    Font = "NonExistent",
                    Page = 1,
                    Encoding = "WinAnsiEncoding"
                }
            });

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*NonExistent*");
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }

    // --- Helper methods to create fixture PDFs ---

    private static string createPdfWithCidFont()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);

            // Build a CIDFontType2 structure manually
            var cidFontDescriptor = new PdfDictionary();
            cidFontDescriptor.Put(PdfName.Type, new PdfName("FontDescriptor"));
            cidFontDescriptor.Put(new PdfName("FontName"), new PdfName("TestCIDFont"));
            cidFontDescriptor.Put(new PdfName("Flags"), new PdfNumber(32));
            cidFontDescriptor.Put(new PdfName("FontBBox"), new PdfArray(new float[] { 0, 0, 1000, 1000 }));
            cidFontDescriptor.Put(new PdfName("ItalicAngle"), new PdfNumber(0));
            cidFontDescriptor.Put(new PdfName("Ascent"), new PdfNumber(800));
            cidFontDescriptor.Put(new PdfName("Descent"), new PdfNumber(-200));
            cidFontDescriptor.Put(new PdfName("CapHeight"), new PdfNumber(700));
            cidFontDescriptor.Put(new PdfName("StemV"), new PdfNumber(80));
            cidFontDescriptor.MakeIndirect(pdf);

            var cidFont = new PdfDictionary();
            cidFont.Put(PdfName.Type, PdfName.Font);
            cidFont.Put(PdfName.Subtype, new PdfName("CIDFontType2"));
            cidFont.Put(new PdfName("BaseFont"), new PdfName("TestCIDFont"));
            cidFont.Put(new PdfName("FontDescriptor"), cidFontDescriptor);
            var cidSystemInfo = new PdfDictionary();
            cidSystemInfo.Put(new PdfName("Registry"), new PdfString("Adobe"));
            cidSystemInfo.Put(new PdfName("Ordering"), new PdfString("Identity"));
            cidSystemInfo.Put(new PdfName("Supplement"), new PdfNumber(0));
            cidFont.Put(new PdfName("CIDSystemInfo"), cidSystemInfo);
            cidFont.MakeIndirect(pdf);

            var descendantFonts = new PdfArray();
            descendantFonts.Add(cidFont);

            var type0Font = new PdfDictionary();
            type0Font.Put(PdfName.Type, PdfName.Font);
            type0Font.Put(PdfName.Subtype, new PdfName("Type0"));
            type0Font.Put(new PdfName("BaseFont"), new PdfName("TestCIDFont"));
            type0Font.Put(PdfName.Encoding, new PdfName("Identity-H"));
            type0Font.Put(new PdfName("DescendantFonts"), descendantFonts);
            type0Font.MakeIndirect(pdf);

            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontResDict == null)
            {
                fontResDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontResDict);
            }
            fontResDict.Put(new PdfName("F1"), type0Font);
        }

        return path;
    }

    private static string createPdfWithType1Font()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);

            // Create a Type1 font dictionary with a FontDescriptor
            var descriptor = new PdfDictionary();
            descriptor.Put(PdfName.Type, new PdfName("FontDescriptor"));
            descriptor.Put(new PdfName("FontName"), new PdfName("TestType1Font"));
            descriptor.Put(new PdfName("Flags"), new PdfNumber(32));
            descriptor.Put(new PdfName("FontBBox"), new PdfArray(new float[] { 0, 0, 1000, 1000 }));
            descriptor.Put(new PdfName("ItalicAngle"), new PdfNumber(0));
            descriptor.Put(new PdfName("Ascent"), new PdfNumber(800));
            descriptor.Put(new PdfName("Descent"), new PdfNumber(-200));
            descriptor.Put(new PdfName("CapHeight"), new PdfNumber(700));
            descriptor.Put(new PdfName("StemV"), new PdfNumber(80));
            descriptor.MakeIndirect(pdf);

            var fontDict = new PdfDictionary();
            fontDict.Put(PdfName.Type, PdfName.Font);
            fontDict.Put(PdfName.Subtype, new PdfName("Type1"));
            fontDict.Put(new PdfName("BaseFont"), new PdfName("TestType1Font"));
            fontDict.Put(new PdfName("FontDescriptor"), descriptor);
            fontDict.MakeIndirect(pdf);

            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontResDict == null)
            {
                fontResDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontResDict);
            }
            fontResDict.Put(new PdfName("F1"), fontDict);
        }

        return path;
    }

    private static string createPdfWithSimpleFont()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);
            var canvas = new PdfCanvas(page);
            var font = PdfFontFactory.CreateFont("Helvetica");
            canvas.BeginText()
                .SetFontAndSize(font, 12)
                .MoveText(72, 700)
                .ShowText("Hello")
                .EndText();

            // The font was registered by iText with some resource name.
            // We need to find the actual font key and ensure "F1" exists.
            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);

            // If iText didn't use "F1", add an alias
            if (fontResDict != null && !fontResDict.ContainsKey(new PdfName("F1")))
            {
                // Find the first font entry and alias it
                foreach (var key in fontResDict.KeySet())
                {
                    var existingFont = fontResDict.Get(key);
                    fontResDict.Put(new PdfName("F1"), existingFont);
                    break;
                }
            }
        }

        return path;
    }

    private static string createPdfWithEncodingDict()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);

            var encodingDict = new PdfDictionary();
            encodingDict.Put(PdfName.Type, PdfName.Encoding);
            encodingDict.Put(new PdfName("BaseEncoding"), new PdfName("WinAnsiEncoding"));

            var fontDict = new PdfDictionary();
            fontDict.Put(PdfName.Type, PdfName.Font);
            fontDict.Put(PdfName.Subtype, new PdfName("TrueType"));
            fontDict.Put(new PdfName("BaseFont"), new PdfName("TestFont"));
            fontDict.Put(PdfName.Encoding, encodingDict);
            fontDict.MakeIndirect(pdf);

            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontResDict == null)
            {
                fontResDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontResDict);
            }
            fontResDict.Put(new PdfName("F1"), fontDict);
        }

        return path;
    }

    private static string createPdfWithWidths()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);

            var widthsArray = new PdfArray();
            // Widths for chars 32-36 (5 entries)
            for (int i = 0; i < 5; i++)
            {
                widthsArray.Add(new PdfNumber(500));
            }

            var fontDict = new PdfDictionary();
            fontDict.Put(PdfName.Type, PdfName.Font);
            fontDict.Put(PdfName.Subtype, new PdfName("TrueType"));
            fontDict.Put(new PdfName("BaseFont"), new PdfName("TestFont"));
            fontDict.Put(new PdfName("FirstChar"), new PdfNumber(32));
            fontDict.Put(new PdfName("LastChar"), new PdfNumber(36));
            fontDict.Put(PdfName.Widths, widthsArray);
            fontDict.MakeIndirect(pdf);

            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontResDict == null)
            {
                fontResDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontResDict);
            }
            fontResDict.Put(new PdfName("F1"), fontDict);
        }

        return path;
    }

    private static string createPdfWithBareFont()
    {
        var path = System.IO.Path.GetTempFileName() + ".pdf";

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.LETTER);

            // Create a font dict without a FontDescriptor
            var fontDict = new PdfDictionary();
            fontDict.Put(PdfName.Type, PdfName.Font);
            fontDict.Put(PdfName.Subtype, new PdfName("TrueType"));
            fontDict.Put(new PdfName("BaseFont"), new PdfName("TestFont"));
            fontDict.MakeIndirect(pdf);

            var resources = page.GetResources();
            var fontResDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontResDict == null)
            {
                fontResDict = new PdfDictionary();
                resources.GetPdfObject().Put(PdfName.Font, fontResDict);
            }
            fontResDict.Put(new PdfName("F1"), fontDict);
        }

        return path;
    }
}
