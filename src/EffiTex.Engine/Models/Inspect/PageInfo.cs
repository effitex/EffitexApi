namespace EffiTex.Engine.Models.Inspect;

public class PageInfo
{
    public int PageNumber { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string TabOrder { get; set; }
    public List<object> ContentOperators { get; set; } = new();
    public List<object> Annotations { get; set; } = new();
    public List<object> Fonts { get; set; } = new();
    public List<int> StructuredMcids { get; set; }
}
