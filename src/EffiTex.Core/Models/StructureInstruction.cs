namespace EffiTex.Core.Models;

public class StructureInstruction
{
    public bool StripExisting { get; set; }
    public string Root { get; set; }
    public List<StructureNode> Children { get; set; }
}
