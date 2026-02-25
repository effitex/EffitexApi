using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class BboxResolverTests
{
    [Fact]
    public void Resolve_BboxAroundKnownText_ReturnsOperatorIndex()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var reader = new PdfReader(source);
        using var pdf = new PdfDocument(reader);

        var resolver = new BboxResolver();
        var page = pdf.GetPage(1);

        // "Hello World" is at position (72, 700), font size 12
        // Approximate bounding box tightly around the text
        var bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 };
        var indices = resolver.Resolve(page, bbox);

        indices.Should().NotBeEmpty();
        indices.Should().Contain(0);
    }

    [Fact]
    public void Resolve_BboxNotOverlappingAnyOperator_ReturnsEmptyList()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var reader = new PdfReader(source);
        using var pdf = new PdfDocument(reader);

        var resolver = new BboxResolver();
        var page = pdf.GetPage(1);

        // Far away from any content
        var bbox = new BoundingBox { X = 500, Y = 100, Width = 10, Height = 10 };
        var indices = resolver.Resolve(page, bbox);

        indices.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_BboxSpanningMultipleOperators_ReturnsAllIndices()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var reader = new PdfReader(source);
        using var pdf = new PdfDocument(reader);

        var resolver = new BboxResolver();
        var page = pdf.GetPage(1);

        // "Hello World" at (72, 700) and "Large Heading Text" at (72, 650)
        // Use a tall bbox that covers both
        var bbox = new BoundingBox { X = 70, Y = 648, Width = 250, Height = 70 };
        var indices = resolver.Resolve(page, bbox);

        indices.Should().HaveCountGreaterThanOrEqualTo(2);
        indices.Should().Contain(0);
        indices.Should().Contain(1);
    }

    [Fact]
    public void Resolve_BboxWithToleranceCatchesOperatorSlightlyOutside()
    {
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            // Create a PDF with text at a precise known location
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(writer))
            {
                var page = pdf.AddNewPage(PageSize.LETTER);
                var canvas = new PdfCanvas(page);
                var font = PdfFontFactory.CreateFont("Helvetica");

                canvas.BeginText()
                    .SetFontAndSize(font, 12)
                    .MoveText(200, 400)
                    .ShowText("Precise")
                    .EndText();

                pdf.Close();
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var resolver = new BboxResolver();
                var page = pdf.GetPage(1);

                // Place bbox 1 point to the left of the text start (x=200).
                // The bbox ends at x=199, and the text starts at x=200, so there
                // is a 1-point gap. The 2-point tolerance should bridge this gap.
                var bbox = new BoundingBox { X = 189, Y = 399, Width = 10, Height = 14 };
                var indices = resolver.Resolve(page, bbox);

                indices.Should().NotBeEmpty("2-point tolerance should catch operators 1 point outside the bbox");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Resolve_ImageOperator_ReturnsImageIndex()
    {
        var source = FixtureGenerator.EnsureScanned();
        using var reader = new PdfReader(source);
        using var pdf = new PdfDocument(reader);

        var resolver = new BboxResolver();
        var page = pdf.GetPage(1);

        // The scanned fixture has an image at (72, 600) with dimensions 200x100
        var bbox = new BoundingBox { X = 72, Y = 600, Width = 200, Height = 100 };
        var indices = resolver.Resolve(page, bbox);

        indices.Should().NotBeEmpty();
    }

    [Fact]
    public void Resolve_EmptyPage_ReturnsEmptyList()
    {
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(writer))
            {
                pdf.AddNewPage(PageSize.LETTER);
                pdf.Close();
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var resolver = new BboxResolver();
                var page = pdf.GetPage(1);

                var bbox = new BoundingBox { X = 72, Y = 700, Width = 100, Height = 20 };
                var indices = resolver.Resolve(page, bbox);

                indices.Should().BeEmpty();
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
