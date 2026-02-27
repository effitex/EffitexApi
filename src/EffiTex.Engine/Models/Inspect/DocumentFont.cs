namespace EffiTex.Engine.Models.Inspect;

public class DocumentFont
{
    public string Name { get; set; }
    public string FontType { get; set; }
    public bool IsEmbedded { get; set; }
    public bool IsSymbolic { get; set; }
    public bool HasTounicode { get; set; }
    public bool HasNotdefGlyph { get; set; }
    public string Encoding { get; set; }
    public bool HasCharset { get; set; }
    public bool HasCidset { get; set; }
    public bool HasFontDescriptor { get; set; }
    public CidSystemInfoData CidSystemInfo { get; set; }
    public CmapInfoData CmapInfo { get; set; }
    public CidToGidMapData CidToGidMap { get; set; }
    public EncodingDetailData EncodingDetail { get; set; }
    public Dictionary<string, string> TounicodeMappings { get; set; }
    public List<int> UnmappableCharCodes { get; set; }
    public Type3FontInfoData Type3Info { get; set; }
    public PdfStreamData FontProgram { get; set; }
    public int[] Pages { get; set; }
}
