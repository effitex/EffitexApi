namespace EffiTex.Core.Models;

public class AnnotationOperation
{
    public string Op { get; set; }
    public int Page { get; set; }
    public int? Index { get; set; }
    public string Value { get; set; }
    public string Node { get; set; }
    public BoundingBox Rect { get; set; }
    public string FieldName { get; set; }
    public string FieldType { get; set; }
    public string Tu { get; set; }
}
