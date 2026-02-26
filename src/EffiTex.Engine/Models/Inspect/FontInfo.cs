namespace EffiTex.Engine.Models.Inspect;

public class FontInfo
{
    public string Name { get; set; }
    public string FontType { get; set; }
    public bool IsEmbedded { get; set; }
    public bool IsSymbolic { get; set; }
    public bool HasTounicode { get; set; }
    public bool HasNotdefGlyph { get; set; }
    public string Encoding { get; set; }
    public int PageNumber { get; set; }
    public bool HasCharset { get; set; }
    public bool HasCidset { get; set; }
    public bool HasFontDescriptor { get; set; }
    public CidSystemInfoData CidSystemInfo { get; set; }
    public CmapInfoData CmapInfo { get; set; }
    public CidToGidMapData CidToGidMap { get; set; }
    public EncodingDetailData EncodingDetail { get; set; }
    public List<CmapSubtableData> CmapSubtables { get; set; }
    public Dictionary<string, string> TounicodeMappings { get; set; }
    public List<int> UnmappableCharCodes { get; set; }
    public Type3FontInfoData Type3Info { get; set; }
    public List<string> Type1GlyphNames { get; set; }
    public string FontProgramData { get; set; }
}

public class CidSystemInfoData
{
    public string Registry { get; set; }
    public string Ordering { get; set; }
    public int Supplement { get; set; }
}

public class CmapInfoData
{
    public string Registry { get; set; }
    public string Ordering { get; set; }
    public int Supplement { get; set; }
    public bool IsEmbedded { get; set; }
    public bool IsPredefined { get; set; }
    public string UseCmapName { get; set; }
    public bool UseCmapIsPredefined { get; set; }
    public int? DictWMode { get; set; }
    public int? StreamWMode { get; set; }
}

public class CidToGidMapData
{
    public bool Present { get; set; }
    public bool IsValid { get; set; }
}

public class EncodingDetailData
{
    public bool IsDictionary { get; set; }
    public string BaseEncoding { get; set; }
    public bool HasDifferencesArray { get; set; }
    public List<string> DifferencesGlyphNames { get; set; }
}

public class CmapSubtableData
{
    public int PlatformId { get; set; }
    public int EncodingId { get; set; }
    public int Format { get; set; }
}

public class Type3FontInfoData
{
    public List<string> CharProcsGlyphNames { get; set; }
    public List<string> EncodedGlyphNames { get; set; }
    public List<int> UsedCharCodes { get; set; }
    public Dictionary<string, string> TounicodeMappings { get; set; }
    public bool HasFontDescriptor { get; set; }
}
