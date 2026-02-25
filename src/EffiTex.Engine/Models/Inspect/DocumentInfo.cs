namespace EffiTex.Engine.Models.Inspect;

public class DocumentInfo
{
    public bool IsTagged { get; set; }
    public int PageCount { get; set; }
    public string PdfVersion { get; set; }
    public string Language { get; set; }
    public string Title { get; set; }
    public bool? DisplayDocTitle { get; set; }
    public bool? MarkInfoMarked { get; set; }
    public bool? SuspectFlag { get; set; }
    public bool HasInfoDictionary { get; set; }
    public bool IsEncrypted { get; set; }
    public string EncryptionPermissions { get; set; }
    public bool HasStructuralParentTree { get; set; }
    public bool HasXfaDynamicRender { get; set; }
}
