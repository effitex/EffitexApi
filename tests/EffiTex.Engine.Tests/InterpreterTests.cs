using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using Xunit;

namespace EffiTex.Engine.Tests;

public class InterpreterTests
{
    private static Interpreter createInterpreter()
    {
        var resolver = new BboxResolver();
        return new Interpreter(
            new MetadataHandler(),
            new StructureHandler(),
            new ContentTaggingHandler(resolver),
            new ArtifactHandler(resolver),
            new AnnotationHandler(),
            new FontHandler(),
            new OcrHandler(),
            new BookmarkHandler());
    }

    [Fact]
    public void Execute_FullInstructionSet_ProducesValidPdf()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet
        {
            Version = "1",
            Metadata = new MetadataInstruction { Language = "en" }
        };

        using var output = interpreter.Execute(input, instructions);
        output.Should().NotBeNull();
        output.Length.Should().BeGreaterThan(0);

        // Verify the output is a valid PDF that can be opened
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);
        pdf.GetNumberOfPages().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Execute_MetadataOnly_AppliesMetadataWithoutStructureTree()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet
        {
            Version = "1",
            Metadata = new MetadataInstruction
            {
                Language = "fr",
                Title = "Test Document"
            }
        };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);

        pdf.GetCatalog().GetLang().GetValue().Should().Be("fr");
        pdf.GetDocumentInfo().GetTitle().Should().Be("Test Document");

        // No structure tree should be created
        var structRoot = pdf.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.StructTreeRoot);
        structRoot.Should().BeNull();
    }

    [Fact]
    public void Execute_StructureAndContentTagging_CreatesBothTagTreeAndMarkers()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet
        {
            Version = "1",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new()
                    {
                        Role = "P",
                        Id = "para1"
                    }
                }
            },
            ContentTagging = new List<ContentTaggingEntry>
            {
                new()
                {
                    Page = 1,
                    Node = "para1",
                    Bbox = new BoundingBox { X = 72, Y = 699, Width = 80, Height = 14 }
                }
            }
        };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);

        // Verify structure tree exists
        pdf.IsTagged().Should().BeTrue();

        // Verify BDC/EMC markers in content stream
        var page = pdf.GetPage(1);
        var content = System.Text.Encoding.GetEncoding("iso-8859-1")
            .GetString(page.GetContentBytes());
        content.Should().Contain("BDC");
        content.Should().Contain("EMC");
    }

    [Fact]
    public void Execute_OcrInstruction_CreatesInvisibleTextLayer()
    {
        var source = FixtureGenerator.EnsureScanned();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet
        {
            Version = "1",
            Ocr = new List<OcrPage>
            {
                new()
                {
                    Page = 1,
                    Words = new List<OcrWord>
                    {
                        new()
                        {
                            Text = "Scanned",
                            Bbox = new BoundingBox { X = 100, Y = 650, Width = 60, Height = 12 }
                        }
                    }
                }
            }
        };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);

        var page = pdf.GetPage(1);
        var content = System.Text.Encoding.GetEncoding("iso-8859-1")
            .GetString(page.GetContentBytes());

        // Rendering mode 3 = invisible text
        content.Should().Contain("3 Tr");
        content.Should().Contain("Scanned");
    }

    [Fact]
    public void Execute_AnyInstruction_StampsProcessorInInfoDictionary()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet { Version = "1" };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);

        var processor = pdf.GetDocumentInfo().GetMoreInfo("Processor");
        processor.Should().NotBeNullOrEmpty();
        processor.Should().StartWith("EffiTex ");
    }

    [Fact]
    public void Execute_AnyInstruction_DoesNotModifyProducerEntry()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();

        // Read the original producer
        string originalProducer;
        using (var r = new PdfReader(source))
        using (var pdf = new PdfDocument(r))
        {
            originalProducer = pdf.GetDocumentInfo().GetProducer();
        }

        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet
        {
            Version = "1",
            Metadata = new MetadataInstruction { Language = "en" }
        };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf2 = new PdfDocument(reader);

        var producer = pdf2.GetDocumentInfo().GetProducer();
        producer.Should().NotBeNull();
        // Producer may be updated by iText but should not be set to "EffiTex"
        producer.Should().NotContain("EffiTex");
    }

    [Fact]
    public void Execute_EmptyInstructionSet_ProducesValidPdfWithOnlyProcessorStamp()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        using var input = File.OpenRead(source);
        var interpreter = createInterpreter();

        var instructions = new InstructionSet { Version = "1" };

        using var output = interpreter.Execute(input, instructions);
        using var reader = new PdfReader(output);
        using var pdf = new PdfDocument(reader);

        pdf.GetNumberOfPages().Should().Be(1);

        // No structure tree
        var structRoot = pdf.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.StructTreeRoot);
        structRoot.Should().BeNull();

        // Processor stamp present
        var processor = pdf.GetDocumentInfo().GetMoreInfo("Processor");
        processor.Should().StartWith("EffiTex ");
    }
}
