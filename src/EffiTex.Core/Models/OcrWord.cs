namespace EffiTex.Core.Models;

public class OcrWord
{
    public string Text { get; set; }
    public BoundingBox Bbox { get; set; }
    public float? Confidence { get; set; }
}
