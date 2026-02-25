namespace EffiTex.Core.Models;

public class OcrPage
{
    public int Page { get; set; }
    public List<OcrWord> Words { get; set; }
}
