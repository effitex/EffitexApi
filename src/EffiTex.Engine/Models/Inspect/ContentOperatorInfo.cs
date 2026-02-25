namespace EffiTex.Engine.Models.Inspect;

public class ContentOperatorInfo
{
    public int ContentStreamIndex { get; set; }
    public string OperatorName { get; set; }
    public bool IsTextOperator { get; set; }
    public bool IsImageOperator { get; set; }
    public bool IsPathOperator { get; set; }
    public bool IsArtifact { get; set; }
    public bool IsInsideMarkedContent { get; set; }
    public int? MarkedContentId { get; set; }
    public string Text { get; set; }
    public string FontName { get; set; }
    public float? FontSize { get; set; }
    public ColorInfo FillColor { get; set; }
    public ColorInfo StrokeColor { get; set; }
    public bool? HasUnicodeMapping { get; set; }
    public string XObjectName { get; set; }
    public string XObjectSubtype { get; set; }
    public int PageNumber { get; set; }
    public BoundingBoxInfo BoundingBox { get; set; }
}

public class ColorInfo
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
}
