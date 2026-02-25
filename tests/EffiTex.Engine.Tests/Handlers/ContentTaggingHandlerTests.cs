using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class ContentTaggingHandlerTests
{
    [Fact]
    public void Apply_KnownTextRegion_InsertsBdcEmcMarkers()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var docElem = new PdfStructElem(pdf, new PdfName("Document"));
                structRoot.AddKid(docElem);
                var h1Elem = new PdfStructElem(pdf, new PdfName("H1"));
                docElem.AddKid(h1Elem);

                var nodeIndex = new Dictionary<string, PdfStructElem>
                {
                    ["h1"] = h1Elem
                };

                var entries = new List<ContentTaggingEntry>
                {
                    new()
                    {
                        Node = "h1",
                        Page = 1,
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    }
                };

                var resolver = new BboxResolver();
                var handler = new ContentTaggingHandler(resolver);
                handler.Apply(pdf, entries, nodeIndex);
            }

            // Re-open and verify BDC/EMC markers exist in the content stream
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var contentBytes = page.GetContentStream(0).GetBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);

                content.Should().Contain("BDC");
                content.Should().Contain("EMC");
                content.Should().Contain("MCID");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_TaggedContent_McidLinksToCorrectStructElement()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var docElem = new PdfStructElem(pdf, new PdfName("Document"));
                structRoot.AddKid(docElem);
                var pElem = new PdfStructElem(pdf, new PdfName("P"));
                docElem.AddKid(pElem);

                var nodeIndex = new Dictionary<string, PdfStructElem>
                {
                    ["para1"] = pElem
                };

                var entries = new List<ContentTaggingEntry>
                {
                    new()
                    {
                        Node = "para1",
                        Page = 1,
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    }
                };

                var resolver = new BboxResolver();
                var handler = new ContentTaggingHandler(resolver);
                handler.Apply(pdf, entries, nodeIndex);
            }

            // Re-open and verify the MCR links to the structure element
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                docElem.Should().NotBeNull();
                var pElem = docElem.GetKids()[0] as PdfStructElem;
                pElem.Should().NotBeNull();
                pElem.GetRole().GetValue().Should().Be("P");

                // The P element should have a /K entry with an MCR
                var k = pElem.GetPdfObject().Get(PdfName.K);
                k.Should().NotBeNull("structure element should have marked content reference");

                // The MCR should have an MCID
                if (k is PdfDictionary mcrDict)
                {
                    mcrDict.GetAsName(PdfName.Type)?.GetValue().Should().Be("MCR");
                    mcrDict.GetAsNumber(new PdfName("MCID")).Should().NotBeNull();
                }
                else if (k is PdfArray kArray)
                {
                    // Find the MCR dict in the array
                    bool foundMcr = false;
                    for (int i = 0; i < kArray.Size(); i++)
                    {
                        var item = kArray.GetAsDictionary(i);
                        if (item != null)
                        {
                            var typeName = item.GetAsName(PdfName.Type);
                            if (typeName != null && typeName.GetValue() == "MCR")
                            {
                                item.GetAsNumber(new PdfName("MCID")).Should().NotBeNull();
                                foundMcr = true;
                                break;
                            }
                        }
                    }
                    foundMcr.Should().BeTrue("should find MCR entry in /K array");
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_BboxMatchingNoOperators_IsNoOp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var docElem = new PdfStructElem(pdf, new PdfName("Document"));
                structRoot.AddKid(docElem);
                var pElem = new PdfStructElem(pdf, new PdfName("P"));
                docElem.AddKid(pElem);

                var nodeIndex = new Dictionary<string, PdfStructElem>
                {
                    ["para1"] = pElem
                };

                // Bbox far from any content
                var entries = new List<ContentTaggingEntry>
                {
                    new()
                    {
                        Node = "para1",
                        Page = 1,
                        Bbox = new BoundingBox { X = 500, Y = 100, Width = 10, Height = 10 }
                    }
                };

                var resolver = new BboxResolver();
                var handler = new ContentTaggingHandler(resolver);
                handler.Apply(pdf, entries, nodeIndex);
            }

            // Verify the output file is valid and the P elem has no MCR
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var pElem = docElem.GetKids()[0] as PdfStructElem;

                // P element should have no /K since no content was matched
                var k = pElem.GetPdfObject().Get(PdfName.K);
                k.Should().BeNull("no content was matched so no MCR should be created");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_MultipleEntriesSamePage_AllApplied()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var docElem = new PdfStructElem(pdf, new PdfName("Document"));
                structRoot.AddKid(docElem);

                var h1Elem = new PdfStructElem(pdf, new PdfName("H1"));
                docElem.AddKid(h1Elem);

                var pElem = new PdfStructElem(pdf, new PdfName("P"));
                docElem.AddKid(pElem);

                var nodeIndex = new Dictionary<string, PdfStructElem>
                {
                    ["heading"] = h1Elem,
                    ["body"] = pElem
                };

                // "Hello World" at (72, 700) and "Large Heading Text" at (72, 650)
                var entries = new List<ContentTaggingEntry>
                {
                    new()
                    {
                        Node = "heading",
                        Page = 1,
                        Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                    },
                    new()
                    {
                        Node = "body",
                        Page = 1,
                        Bbox = new BoundingBox { X = 72, Y = 648, Width = 250, Height = 28 }
                    }
                };

                var resolver = new BboxResolver();
                var handler = new ContentTaggingHandler(resolver);
                handler.Apply(pdf, entries, nodeIndex);
            }

            // Re-open and verify both elements got MCRs
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                docElem.Should().NotBeNull();

                var h1Elem = docElem.GetKids()[0] as PdfStructElem;
                h1Elem.Should().NotBeNull();
                h1Elem.GetRole().GetValue().Should().Be("H1");
                h1Elem.GetPdfObject().Get(PdfName.K).Should().NotBeNull(
                    "H1 element should have marked content reference");

                var pElem = docElem.GetKids()[1] as PdfStructElem;
                pElem.Should().NotBeNull();
                pElem.GetRole().GetValue().Should().Be("P");
                pElem.GetPdfObject().Get(PdfName.K).Should().NotBeNull(
                    "P element should have marked content reference");

                // Verify content stream has multiple BDC markers
                var page = pdf.GetPage(1);
                var contentBytes = page.GetContentStream(0).GetBytes();
                var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);

                var bdcCount = content.Split("BDC").Length - 1;
                bdcCount.Should().BeGreaterThanOrEqualTo(2, "both entries should produce BDC markers");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_NullEntries_IsNoOp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var resolver = new BboxResolver();
                var handler = new ContentTaggingHandler(resolver);
                handler.Apply(pdf, null, new Dictionary<string, PdfStructElem>());
            }

            File.Exists(temp).Should().BeTrue();
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
