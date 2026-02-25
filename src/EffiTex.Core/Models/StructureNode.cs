namespace EffiTex.Core.Models;

public class StructureNode
{
    public string Id { get; set; }
    public string Role { get; set; }
    public BoundingBox Bbox { get; set; }
    public string Language { get; set; }
    public string AltText { get; set; }
    public string ActualText { get; set; }
    public string ElementId { get; set; }
    public string Scope { get; set; }
    public int? ColSpan { get; set; }
    public int? RowSpan { get; set; }
    public List<StructureAttribute> Attributes { get; set; }
    public List<StructureNode> Children { get; set; } = new();
}
