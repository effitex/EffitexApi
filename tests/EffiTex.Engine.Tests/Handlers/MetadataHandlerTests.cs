using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class MetadataHandlerTests
{
    [Fact]
    public void Apply_FullInstruction_SetsAllFields()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction
                {
                    Language = "en-US",
                    Title = "Full Test Document",
                    DisplayDocTitle = true,
                    MarkInfo = true,
                    PdfUaIdentifier = 1,
                    TabOrder = "structure"
                });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                // Language
                pdf.GetCatalog().GetLang().GetValue().Should().Be("en-US");

                // Title in Info dict
                pdf.GetDocumentInfo().GetTitle().Should().Be("Full Test Document");

                // Title in XMP
                var xmp = pdf.GetXmpMetadata(false);
                xmp.Should().NotBeNull();
                var titleProp = xmp.GetLocalizedText("http://purl.org/dc/elements/1.1/", "title", "", "x-default");
                titleProp.GetValue().Should().Be("Full Test Document");

                // DisplayDocTitle
                var catalog = pdf.GetCatalog().GetPdfObject();
                var vp = catalog.GetAsDictionary(PdfName.ViewerPreferences);
                vp.Should().NotBeNull();
                vp.GetAsBoolean(new PdfName("DisplayDocTitle")).GetValue().Should().BeTrue();

                // MarkInfo
                var markInfo = catalog.GetAsDictionary(PdfName.MarkInfo);
                markInfo.Should().NotBeNull();
                markInfo.GetAsBoolean(PdfName.Marked).GetValue().Should().BeTrue();
                markInfo.GetAsBoolean(new PdfName("Suspect")).GetValue().Should().BeFalse();

                // PdfUaIdentifier in XMP
                var pdfuaPart = xmp.GetPropertyInteger("http://www.aiim.org/pdfua/ns/id/", "part");
                pdfuaPart.Should().Be(1);

                // Tab order on all pages
                for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
                {
                    pdf.GetPage(i).GetPdfObject().GetAsName(PdfName.Tabs).GetValue().Should().Be("S");
                }
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_Language_SetsCatalogLang()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { Language = "en-US" });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                pdf.GetCatalog().GetLang().GetValue().Should().Be("en-US");
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_Title_SetsInfoDictAndXmp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { Title = "My Document Title" });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                pdf.GetDocumentInfo().GetTitle().Should().Be("My Document Title");

                var xmp = pdf.GetXmpMetadata(false);
                xmp.Should().NotBeNull();
                var titleProp = xmp.GetLocalizedText("http://purl.org/dc/elements/1.1/", "title", "", "x-default");
                titleProp.GetValue().Should().Be("My Document Title");
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_DisplayDocTitle_SetsViewerPreferences()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { DisplayDocTitle = true });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                var catalog = pdf.GetCatalog().GetPdfObject();
                var vp = catalog.GetAsDictionary(PdfName.ViewerPreferences);
                vp.Should().NotBeNull();
                vp.GetAsBoolean(new PdfName("DisplayDocTitle")).GetValue().Should().BeTrue();
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_MarkInfo_SetsMarkInfoDictionary()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { MarkInfo = true });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                var catalog = pdf.GetCatalog().GetPdfObject();
                var markInfo = catalog.GetAsDictionary(PdfName.MarkInfo);
                markInfo.Should().NotBeNull();
                markInfo.GetAsBoolean(PdfName.Marked).GetValue().Should().BeTrue();
                markInfo.GetAsBoolean(new PdfName("Suspect")).GetValue().Should().BeFalse();
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_PdfUaIdentifier_WritesXmpPdfuaidPart()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { PdfUaIdentifier = 1 });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                var xmp = pdf.GetXmpMetadata(false);
                xmp.Should().NotBeNull();
                var pdfuaPart = xmp.GetPropertyInteger("http://www.aiim.org/pdfua/ns/id/", "part");
                pdfuaPart.Should().Be(1);
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_TabOrderStructure_SetsTabsSOnAllPages()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, new MetadataInstruction { TabOrder = "structure" });
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
                {
                    pdf.GetPage(i).GetPdfObject().GetAsName(PdfName.Tabs).GetValue().Should().Be("S");
                }
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }

    [Fact]
    public void Apply_NullInstruction_IsNoOp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var tempOutput = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(tempOutput))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new MetadataHandler();
                handler.Apply(pdf, null);
            }

            using (var reader = new PdfReader(tempOutput))
            using (var pdf = new PdfDocument(reader))
            {
                // Should not have any metadata set
                pdf.GetCatalog().GetLang().Should().BeNull();
                pdf.GetDocumentInfo().GetTitle().Should().BeNull();
            }
        }
        finally
        {
            File.Delete(tempOutput);
        }
    }
}
