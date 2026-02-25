using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class ArtifactHandlerTests
{
    [Fact]
    public void Apply_NullEntries_DoesNotModifyPdf()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, null);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().NotContain("BDC");
                content.Should().NotContain("EMC");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_LayoutArtifact_WrapsOperatorsInBdcEmc()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, new List<ArtifactEntry>
                {
                    new()
                    {
                        Page = 1,
                        Type = "layout",
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().Contain("/Artifact <</Type /Layout>> BDC");
                content.Should().Contain("EMC");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_HeaderType_UsesCorrectTypeName()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, new List<ArtifactEntry>
                {
                    new()
                    {
                        Page = 1,
                        Type = "header",
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().Contain("/Artifact <</Type /Header>> BDC");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_UnknownType_DefaultsToLayout()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, new List<ArtifactEntry>
                {
                    new()
                    {
                        Page = 1,
                        Type = "unknown_type",
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().Contain("/Artifact <</Type /Layout>> BDC");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_BboxMatchingNoOperators_ContentStreamUnchanged()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            string originalContent;
            using (var reader = new PdfReader(source))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                originalContent = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
            }

            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, new List<ArtifactEntry>
                {
                    new()
                    {
                        Page = 1,
                        Type = "footer",
                        Bbox = new BoundingBox { X = 500, Y = 100, Width = 10, Height = 10 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().NotContain("BDC");
                content.Should().NotContain("EMC");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_FooterAndPaginationTypes_MarksEachCorrectly()
    {
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        var source = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            // Create a PDF with two text blocks at known positions
            using (var writer = new PdfWriter(source))
            using (var pdf = new PdfDocument(writer))
            {
                var page = pdf.AddNewPage(PageSize.LETTER);
                var canvas = new PdfCanvas(page);
                var font = PdfFontFactory.CreateFont("Helvetica");

                canvas.BeginText()
                    .SetFontAndSize(font, 10)
                    .MoveText(72, 50)
                    .ShowText("Footer text")
                    .EndText();

                canvas.BeginText()
                    .SetFontAndSize(font, 10)
                    .MoveText(500, 50)
                    .ShowText("Page 1")
                    .EndText();
            }

            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new ArtifactHandler(new BboxResolver());
                handler.Apply(pdf, new List<ArtifactEntry>
                {
                    new()
                    {
                        Page = 1,
                        Type = "footer",
                        Bbox = new BoundingBox { X = 70, Y = 48, Width = 100, Height = 14 }
                    },
                    new()
                    {
                        Page = 1,
                        Type = "pagination",
                        Bbox = new BoundingBox { X = 498, Y = 48, Width = 60, Height = 14 }
                    }
                });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var content = System.Text.Encoding.GetEncoding("iso-8859-1")
                    .GetString(page.GetContentBytes());
                content.Should().Contain("/Artifact <</Type /Footer>> BDC");
                content.Should().Contain("/Artifact <</Type /Pagination>> BDC");
            }
        }
        finally
        {
            File.Delete(source);
            File.Delete(temp);
        }
    }
}
