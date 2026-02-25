namespace EffiTex.Engine.Models.Inspect;

public class OutlineNode
{
    public string Title { get; set; }
    public string Lang { get; set; }
    public List<OutlineNode> Children { get; set; }
}
