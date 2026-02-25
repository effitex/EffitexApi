namespace EffiTex.Core.Models;

public class InstructionSet
{
    public string Version { get; set; }
    public MetadataInstruction Metadata { get; set; }
    public StructureInstruction Structure { get; set; }
    public List<ContentTaggingEntry> ContentTagging { get; set; }
    public List<ArtifactEntry> Artifacts { get; set; }
    public List<AnnotationOperation> Annotations { get; set; }
    public BookmarksInstruction Bookmarks { get; set; }
    public List<FontOperation> Fonts { get; set; }
    public List<OcrPage> Ocr { get; set; }
}
