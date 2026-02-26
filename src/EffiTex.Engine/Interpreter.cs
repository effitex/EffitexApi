using System.Reflection;
using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;

namespace EffiTex.Engine;

public class Interpreter
{
    private readonly MetadataHandler _metadata;
    private readonly StructureHandler _structure;
    private readonly ContentTaggingHandler _contentTagging;
    private readonly ArtifactHandler _artifact;
    private readonly AnnotationHandler _annotation;
    private readonly FontHandler _font;
    private readonly OcrHandler _ocr;
    private readonly BookmarkHandler _bookmark;

    public Interpreter(
        MetadataHandler metadata,
        StructureHandler structure,
        ContentTaggingHandler contentTagging,
        ArtifactHandler artifact,
        AnnotationHandler annotation,
        FontHandler font,
        OcrHandler ocr,
        BookmarkHandler bookmark)
    {
        _metadata = metadata;
        _structure = structure;
        _contentTagging = contentTagging;
        _artifact = artifact;
        _annotation = annotation;
        _font = font;
        _ocr = ocr;
        _bookmark = bookmark;
    }

    public Stream Execute(Stream inputPdf, InstructionSet instructions)
    {
        var output = new MemoryStream();

        using (var reader = new PdfReader(inputPdf))
        using (var writer = new PdfWriter(output))
        using (var pdf = new PdfDocument(reader, writer))
        {
            writer.SetCloseStream(false);

            // 1. Metadata
            _metadata.Apply(pdf, instructions.Metadata);

            // 2. Structure (returns node index for downstream handlers)
            var nodeIndex = _structure.Apply(pdf, instructions.Structure);

            // 3. Content tagging (uses node index)
            _contentTagging.Apply(pdf, instructions.ContentTagging, nodeIndex);

            // 4. Artifacts
            _artifact.Apply(pdf, instructions.Artifacts);

            // 5. Annotations (uses node index)
            _annotation.Apply(pdf, instructions.Annotations, nodeIndex);

            // 6. Fonts
            _font.Apply(pdf, instructions.Fonts);

            // 7. OCR
            _ocr.Apply(pdf, instructions.Ocr);

            // 8. Bookmarks
            _bookmark.Apply(pdf, instructions.Bookmarks);

            // 9. Stamp Processor
            stampProcessor(pdf);

            // 10. Save (implicit via PdfDocument.Close)
        }

        output.Position = 0;
        return output;
    }

    private static void stampProcessor(PdfDocument pdf)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = version != null ? version.ToString() : "0.0.0";
        var info = pdf.GetDocumentInfo();
        info.SetMoreInfo("Processor", $"EffiTex {versionString} (https://github.com/effitex/EffiTexApi)");
    }
}
