using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class BookmarkHandlerTests
{
    [Fact]
    public void Apply_StructuredHeadings_CreatesOutlineTree()
    {
        var source = FixtureGenerator.EnsureStructuredHeadings();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new BookmarkHandler();
                handler.Apply(pdf, new BookmarksInstruction { GenerateFromHeadings = true });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var outlines = pdf.GetOutlines(false);
                outlines.Should().NotBeNull();

                var topLevel = outlines.GetAllChildren();
                topLevel.Should().NotBeEmpty();

                // The structured PDF has: H1("Chapter One"), H2("Section 1.1"), H3("Subsection 1.1.1")
                // H1 is the top-level entry
                topLevel.Should().HaveCount(1);
                topLevel[0].GetTitle().Should().Be("Chapter One");

                // H2 is nested under H1
                var h1Children = topLevel[0].GetAllChildren();
                h1Children.Should().HaveCount(1);
                h1Children[0].GetTitle().Should().Be("Section 1.1");

                // H3 is nested under H2
                var h2Children = h1Children[0].GetAllChildren();
                h2Children.Should().HaveCount(1);
                h2Children[0].GetTitle().Should().Be("Subsection 1.1.1");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_StructuredHeadings_OutlinesLinkToCorrectPages()
    {
        var source = FixtureGenerator.EnsureStructuredHeadings();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new BookmarkHandler();
                handler.Apply(pdf, new BookmarksInstruction { GenerateFromHeadings = true });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var outlines = pdf.GetOutlines(false);
                var topLevel = outlines.GetAllChildren();

                // H1 "Chapter One" is on page 1
                var h1Dest = topLevel[0].GetDestination();
                h1Dest.Should().NotBeNull();
                var h1DestObj = (PdfArray)h1Dest.GetPdfObject();
                var h1PageRef = h1DestObj.GetAsDictionary(0);
                var page1Ref = pdf.GetPage(1).GetPdfObject();
                h1PageRef.Should().BeSameAs(page1Ref);

                // H2 "Section 1.1" is on page 1
                var h2 = topLevel[0].GetAllChildren()[0];
                var h2Dest = h2.GetDestination();
                h2Dest.Should().NotBeNull();
                var h2DestObj = (PdfArray)h2Dest.GetPdfObject();
                var h2PageRef = h2DestObj.GetAsDictionary(0);
                h2PageRef.Should().BeSameAs(page1Ref);

                // H3 "Subsection 1.1.1" is on page 2
                var h3 = h2.GetAllChildren()[0];
                var h3Dest = h3.GetDestination();
                h3Dest.Should().NotBeNull();
                var h3DestObj = (PdfArray)h3Dest.GetPdfObject();
                var h3PageRef = h3DestObj.GetAsDictionary(0);
                var page2Ref = pdf.GetPage(2).GetPdfObject();
                h3PageRef.Should().BeSameAs(page2Ref);
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_GenerateFromHeadingsFalse_IsNoOp()
    {
        var source = FixtureGenerator.EnsureStructuredHeadings();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new BookmarkHandler();
                handler.Apply(pdf, new BookmarksInstruction { GenerateFromHeadings = false });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var outlines = pdf.GetOutlines(false);
                if (outlines != null)
                {
                    var children = outlines.GetAllChildren();
                    children.Should().BeEmpty();
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_PdfWithNoHeadings_ProducesNoOutlines()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new BookmarkHandler();
                handler.Apply(pdf, new BookmarksInstruction { GenerateFromHeadings = true });
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var outlines = pdf.GetOutlines(false);
                if (outlines != null)
                {
                    var children = outlines.GetAllChildren();
                    children.Should().BeEmpty();
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_NullInstruction_IsNoOp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new BookmarkHandler();
                handler.Apply(pdf, null);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var outlines = pdf.GetOutlines(false);
                if (outlines != null)
                {
                    var children = outlines.GetAllChildren();
                    children.Should().BeEmpty();
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
