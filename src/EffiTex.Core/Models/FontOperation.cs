namespace EffiTex.Core.Models;

public class FontOperation
{
    public string Op { get; set; }
    public string Font { get; set; }
    public int Page { get; set; }
    public List<int> Cids { get; set; }
    public List<string> GlyphNames { get; set; }
    public string Encoding { get; set; }
    public Dictionary<int, string> Differences { get; set; }
    public Dictionary<int, string> Mappings { get; set; }
    public Dictionary<int, float> Widths { get; set; }
}
