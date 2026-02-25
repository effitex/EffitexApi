namespace EffiTex.Core.Models;

public class MetadataInstruction
{
    public string Language { get; set; }
    public string Title { get; set; }
    public bool? DisplayDocTitle { get; set; }
    public bool? MarkInfo { get; set; }
    public int? PdfUaIdentifier { get; set; }
    public string TabOrder { get; set; }
}
