namespace EffiTex.Engine.Models.Inspect;

public class InspectResponse
{
    public string FileHash { get; set; }
    public long FileSizeBytes { get; set; }
    public DocumentInfo Document { get; set; }
    public string XmpMetadata { get; set; }
    public List<DocumentFont> Fonts { get; set; } = new();
    public List<StructureTreeNode> StructureTree { get; set; }
    public Dictionary<string, string> RoleMap { get; set; }
    public List<PageInfo> Pages { get; set; }
    public List<OutlineNode> Outlines { get; set; }
    public List<EmbeddedFileInfo> EmbeddedFiles { get; set; }
    public List<OcgConfigInfo> OcgConfigurations { get; set; }
    public List<ColorPairInfo> ColorPairs { get; set; } = new();
}
