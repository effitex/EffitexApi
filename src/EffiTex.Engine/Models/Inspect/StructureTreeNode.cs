namespace EffiTex.Engine.Models.Inspect;

public class StructureTreeNode
{
    public string Role { get; set; }
    public int? Page { get; set; }
    public int? Mcid { get; set; }
    public string Id { get; set; }
    public string AltText { get; set; }
    public string ActualText { get; set; }
    public string Language { get; set; }
    public bool HasBbox { get; set; }
    public Dictionary<string, object> Attributes { get; set; }
    public List<StructureTreeNode> Children { get; set; }
}
