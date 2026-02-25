namespace EffiTex.Engine.Models.Inspect;

public class AnnotationInfo
{
    public string Subtype { get; set; }
    public int Index { get; set; }
    public int PageNumber { get; set; }
    public BoundingBoxInfo BoundingBox { get; set; }
    public string Contents { get; set; }
    public string Uri { get; set; }
    public string Tu { get; set; }
    public string AltText { get; set; }
    public bool IsHidden { get; set; }
    public string EnclosingTagRole { get; set; }
    public bool HasTabOrder { get; set; }
    public string FieldName { get; set; }
    public string FieldType { get; set; }
    public int? FieldFlags { get; set; }
    public string FieldValue { get; set; }
}
