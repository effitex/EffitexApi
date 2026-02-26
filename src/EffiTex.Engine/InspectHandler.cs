using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EffiTex.Engine.Models.Inspect;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Tagging;
using iText.Kernel.XMP;
using iText.Kernel.XMP.Options;

namespace EffiTex.Engine;

public class InspectHandler
{
    public InspectResponse Inspect(Stream pdfStream)
    {
        var streamBytes = readAllBytes(pdfStream);
        var fileHash = computeSha256(streamBytes);

        using var memStream = new MemoryStream(streamBytes);
        using var reader = new PdfReader(memStream);
        using var pdf = new PdfDocument(reader);

        var (pages, docFonts) = readPages(pdf);

        var response = new InspectResponse
        {
            FileHash = fileHash,
            FileSizeBytes = streamBytes.Length,
            Document = buildDocumentInfo(pdf),
            XmpMetadata = readXmpMetadata(pdf),
            Fonts = docFonts,
            StructureTree = readStructureTree(pdf),
            RoleMap = readRoleMap(pdf),
            Pages = pages,
            Outlines = readOutlines(pdf),
            EmbeddedFiles = readEmbeddedFiles(pdf),
            OcgConfigurations = readOcgConfigurations(pdf)
        };

        return response;
    }

    private static byte[] readAllBytes(Stream stream)
    {
        if (stream is MemoryStream ms && ms.Position == 0)
            return ms.ToArray();

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string computeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static DocumentInfo buildDocumentInfo(PdfDocument pdf)
    {
        var catalog = pdf.GetCatalog().GetPdfObject();
        var info = new DocumentInfo
        {
            IsTagged = pdf.IsTagged(),
            PageCount = pdf.GetNumberOfPages(),
            PdfVersion = pdf.GetPdfVersion()?.ToString(),
            Language = pdf.GetCatalog().GetLang()?.GetValue(),
            Title = pdf.GetDocumentInfo()?.GetTitle(),
            HasInfoDictionary = pdf.GetTrailer()?.GetAsDictionary(PdfName.Info) != null,
            IsEncrypted = pdf.GetReader()?.IsEncrypted() ?? false,
            EncryptionPermissions = null
        };

        // DisplayDocTitle
        var vp = catalog.GetAsDictionary(PdfName.ViewerPreferences);
        if (vp != null)
        {
            var displayDocTitle = vp.GetAsBoolean(new PdfName("DisplayDocTitle"));
            info.DisplayDocTitle = displayDocTitle?.GetValue();
        }

        // MarkInfo
        var markInfo = catalog.GetAsDictionary(PdfName.MarkInfo);
        if (markInfo != null)
        {
            var marked = markInfo.GetAsBoolean(PdfName.Marked);
            info.MarkInfoMarked = marked?.GetValue();
            var suspect = markInfo.GetAsBoolean(new PdfName("Suspect"));
            info.SuspectFlag = suspect?.GetValue();
        }

        // EncryptionPermissions
        if (info.IsEncrypted)
        {
            try
            {
                var permissions = pdf.GetReader().GetPermissions();
                info.EncryptionPermissions = permissions.ToString();
            }
            catch
            {
                // Ignore permission read errors
            }
        }

        // HasStructuralParentTree
        var structTreeRoot = catalog.GetAsDictionary(PdfName.StructTreeRoot);
        if (structTreeRoot != null)
        {
            info.HasStructuralParentTree = structTreeRoot.Get(new PdfName("ParentTree")) != null;
        }

        // HasXfaDynamicRender
        var acroForm = catalog.GetAsDictionary(new PdfName("AcroForm"));
        if (acroForm != null)
        {
            info.HasXfaDynamicRender = acroForm.Get(new PdfName("XFA")) != null;
        }

        return info;
    }

    private static string readXmpMetadata(PdfDocument pdf)
    {
        var xmp = pdf.GetXmpMetadata(false);
        if (xmp == null)
            return null;
        var xmpBytes = XMPMetaFactory.SerializeToBuffer(xmp, new SerializeOptions());
        if (xmpBytes == null || xmpBytes.Length == 0)
            return null;
        return Convert.ToBase64String(xmpBytes);
    }

    private static List<StructureTreeNode> readStructureTree(PdfDocument pdf)
    {
        var result = new List<StructureTreeNode>();
        if (!pdf.IsTagged()) return result;

        var structRoot = pdf.GetStructTreeRoot();
        if (structRoot == null) return result;

        var kids = structRoot.GetKids();
        if (kids == null) return result;

        foreach (var kid in kids)
        {
            if (kid is PdfStructElem elem)
            {
                result.Add(buildStructureTreeNode(pdf, elem));
            }
        }

        return result;
    }

    private static StructureTreeNode buildStructureTreeNode(PdfDocument pdf, PdfStructElem elem)
    {
        var dict = elem.GetPdfObject();
        var node = new StructureTreeNode
        {
            Role = elem.GetRole()?.GetValue(),
            Children = new List<StructureTreeNode>(),
            Attributes = new Dictionary<string, object>()
        };

        // Id
        var idObj = dict.GetAsString(new PdfName("ID"));
        node.Id = idObj?.GetValue();

        // AltText
        var altObj = dict.GetAsString(PdfName.Alt);
        node.AltText = altObj?.GetValue();

        // ActualText
        var actualTextObj = dict.GetAsString(new PdfName("ActualText"));
        node.ActualText = actualTextObj?.GetValue();

        // Language
        var langObj = dict.GetAsString(PdfName.Lang);
        node.Language = langObj?.GetValue();

        // HasBbox - check /A attribute for BBox
        node.HasBbox = checkHasBbox(dict);

        // Read attributes from /A
        readAttributes(dict, node.Attributes);

        // Page and Mcid from /K and /Pg
        readPageAndMcid(pdf, dict, node);

        // Children
        var kids = elem.GetKids();
        if (kids != null)
        {
            foreach (var kid in kids)
            {
                if (kid is PdfStructElem childElem)
                {
                    node.Children.Add(buildStructureTreeNode(pdf, childElem));
                }
            }
        }

        return node;
    }

    private static void readPageAndMcid(PdfDocument pdf, PdfDictionary dict, StructureTreeNode node)
    {
        // Try to get page from /Pg
        var pgObj = dict.GetAsDictionary(PdfName.Pg);
        if (pgObj != null)
        {
            node.Page = findPageNumber(pdf, pgObj);
        }

        // Get MCID from /K
        var k = dict.Get(PdfName.K);
        if (k is PdfNumber kNum)
        {
            node.Mcid = kNum.IntValue();
            // If no page yet, try from parent tree context
        }
        else if (k is PdfDictionary kDict)
        {
            var mcidObj = kDict.GetAsNumber(new PdfName("MCID"));
            if (mcidObj != null)
                node.Mcid = mcidObj.IntValue();
            var kPgObj = kDict.GetAsDictionary(PdfName.Pg);
            if (kPgObj != null && node.Page == null)
                node.Page = findPageNumber(pdf, kPgObj);
        }
        else if (k is PdfArray kArr && kArr.Size() > 0)
        {
            // Take first MCR entry
            var first = kArr.Get(0);
            if (first is PdfNumber firstNum)
            {
                node.Mcid = firstNum.IntValue();
            }
            else if (first is PdfDictionary firstDict)
            {
                var mcidObj = firstDict.GetAsNumber(new PdfName("MCID"));
                if (mcidObj != null)
                    node.Mcid = mcidObj.IntValue();
                var kPgObj = firstDict.GetAsDictionary(PdfName.Pg);
                if (kPgObj != null && node.Page == null)
                    node.Page = findPageNumber(pdf, kPgObj);
            }
        }
    }

    private static bool checkHasBbox(PdfDictionary dict)
    {
        var a = dict.Get(PdfName.A);
        if (a == null) return false;

        if (a is PdfDictionary aDict)
        {
            return aDict.Get(new PdfName("BBox")) != null;
        }

        if (a is PdfArray aArr)
        {
            for (int i = 0; i < aArr.Size(); i++)
            {
                var item = aArr.GetAsDictionary(i);
                if (item != null && item.Get(new PdfName("BBox")) != null)
                    return true;
            }
        }

        return false;
    }

    private static void readAttributes(PdfDictionary dict, Dictionary<string, object> attributes)
    {
        var a = dict.Get(PdfName.A);
        if (a == null) return;

        if (a is PdfDictionary aDict)
        {
            extractAttributeDict(aDict, attributes);
        }
        else if (a is PdfArray aArr)
        {
            for (int i = 0; i < aArr.Size(); i++)
            {
                var item = aArr.GetAsDictionary(i);
                if (item != null)
                    extractAttributeDict(item, attributes);
            }
        }
    }

    private static void extractAttributeDict(PdfDictionary attrDict, Dictionary<string, object> attributes)
    {
        var owner = attrDict.GetAsName(PdfName.O)?.GetValue();
        foreach (var key in attrDict.KeySet())
        {
            var keyName = key.GetValue();
            if (keyName == "O") continue;

            var prefix = owner != null ? $"{owner}:" : "";
            var val = attrDict.Get(key);
            attributes[$"{prefix}{keyName}"] = pdfObjectToClr(val);
        }
    }

    private static object pdfObjectToClr(PdfObject obj)
    {
        if (obj is PdfString s) return s.GetValue();
        if (obj is PdfName n) return n.GetValue();
        if (obj is PdfNumber num) return num.DoubleValue();
        if (obj is PdfBoolean b) return b.GetValue();
        if (obj is PdfArray arr)
        {
            var list = new List<object>();
            for (int i = 0; i < arr.Size(); i++)
                list.Add(pdfObjectToClr(arr.Get(i)));
            return list;
        }
        return obj?.ToString();
    }

    private static int? findPageNumber(PdfDocument pdf, PdfDictionary pageObj)
    {
        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            if (pdf.GetPage(i).GetPdfObject() == pageObj)
                return i;
        }
        return null;
    }

    private static Dictionary<string, string> readRoleMap(PdfDocument pdf)
    {
        var result = new Dictionary<string, string>();
        if (!pdf.IsTagged()) return result;

        var structRoot = pdf.GetStructTreeRoot();
        if (structRoot == null) return result;

        var roleMap = structRoot.GetRoleMap();
        if (roleMap == null) return result;

        foreach (var entry in roleMap.EntrySet())
        {
            var key = entry.Key.GetValue();
            var value = (entry.Value as PdfName)?.GetValue();
            if (value != null)
                result[key] = value;
        }

        return result;
    }

    private static (List<PageInfo> pages, List<DocumentFont> docFonts) readPages(PdfDocument pdf)
    {
        var pages = new List<PageInfo>();
        var allOccurrences = new List<(int pageNum, FontInfo info)>();

        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            var page = pdf.GetPage(i);
            var pageDict = page.GetPdfObject();
            var mediaBox = page.GetMediaBox();

            var tabOrder = pageDict.GetAsName(PdfName.Tabs)?.GetValue();
            var pageFonts = readPageFonts(pdf, i);

            var pageInfo = new PageInfo
            {
                PageNumber = i,
                Width = mediaBox.GetWidth(),
                Height = mediaBox.GetHeight(),
                TabOrder = tabOrder,
                Fonts = pageFonts.Select(f => f.Name).ToList(),
                StructuredMcids = readStructuredMcids(pdf, i)
            };

            pages.Add(pageInfo);

            foreach (var fontInfo in pageFonts)
                allOccurrences.Add((i, fontInfo));
        }

        var docFonts = allOccurrences
            .GroupBy(o => o.info.Name)
            .Select(g =>
            {
                var first = g.First().info;
                return new DocumentFont
                {
                    Name = first.Name,
                    FontType = first.FontType,
                    IsEmbedded = first.IsEmbedded,
                    IsSymbolic = first.IsSymbolic,
                    HasTounicode = first.HasTounicode,
                    HasNotdefGlyph = first.HasNotdefGlyph,
                    Encoding = first.Encoding,
                    HasCharset = first.HasCharset,
                    HasCidset = first.HasCidset,
                    HasFontDescriptor = first.HasFontDescriptor,
                    CidSystemInfo = first.CidSystemInfo,
                    CmapInfo = first.CmapInfo,
                    CidToGidMap = first.CidToGidMap,
                    EncodingDetail = first.EncodingDetail,
                    CmapSubtables = first.CmapSubtables,
                    TounicodeMappings = first.TounicodeMappings,
                    UnmappableCharCodes = first.UnmappableCharCodes,
                    Type3Info = first.Type3Info,
                    Type1GlyphNames = first.Type1GlyphNames,
                    FontProgramData = first.FontProgramData,
                    Pages = g.Select(o => o.pageNum).OrderBy(p => p).ToArray()
                };
            })
            .ToList();

        return (pages, docFonts);
    }

    private static List<FontInfo> readPageFonts(PdfDocument pdf, int pageNumber)
    {
        var fonts = new List<FontInfo>();
        var page = pdf.GetPage(pageNumber);
        var pageDict = page.GetPdfObject();

        var resources = pageDict.GetAsDictionary(PdfName.Resources);
        if (resources == null) return fonts;

        var fontDict = resources.GetAsDictionary(PdfName.Font);
        if (fontDict == null) return fonts;

        foreach (var entry in fontDict.EntrySet())
        {
            var resourceKey = entry.Key.GetValue();
            var fontObj = entry.Value;

            if (fontObj is PdfIndirectReference indirectRef)
                fontObj = indirectRef.GetRefersTo();

            if (fontObj is not PdfDictionary fd)
                continue;

            var fontInfo = buildFontInfo(fd, resourceKey, pageNumber);
            fonts.Add(fontInfo);
        }

        return fonts;
    }

    private static FontInfo buildFontInfo(PdfDictionary fontDict, string resourceKey, int pageNumber)
    {
        var subtype = fontDict.GetAsName(PdfName.Subtype)?.GetValue();
        var baseFont = fontDict.GetAsName(PdfName.BaseFont)?.GetValue();
        var name = baseFont ?? resourceKey;

        var fontDescriptor = fontDict.GetAsDictionary(PdfName.FontDescriptor);
        var hasFontDescriptor = fontDescriptor != null;
        var isEmbedded = checkIsEmbedded(fontDescriptor);
        var isSymbolic = checkIsSymbolic(fontDescriptor);
        var hasTounicode = fontDict.Get(PdfName.ToUnicode) != null;
        var hasCharset = fontDescriptor?.Get(new PdfName("CharSet")) != null;
        var hasCidset = fontDescriptor?.Get(new PdfName("CIDSet")) != null;
        var encoding = readFontEncoding(fontDict);
        var encodingDetail = readEncodingDetail(fontDict);

        var fontInfo = new FontInfo
        {
            Name = name,
            FontType = subtype,
            IsEmbedded = isEmbedded,
            IsSymbolic = isSymbolic,
            HasTounicode = hasTounicode,
            HasNotdefGlyph = false,
            Encoding = encoding,
            PageNumber = pageNumber,
            HasCharset = hasCharset,
            HasCidset = hasCidset,
            HasFontDescriptor = hasFontDescriptor,
            EncodingDetail = encodingDetail,
            TounicodeMappings = hasTounicode ? parseTounicode(fontDict) : new Dictionary<string, string>(),
            UnmappableCharCodes = new List<int>(),
            CmapSubtables = new List<CmapSubtableData>(),
            Type1GlyphNames = new List<string>()
        };

        if (subtype == "Type0")
        {
            readType0FontInfo(fontDict, fontInfo);
        }
        else if (subtype == "Type3")
        {
            fontInfo.Type3Info = readType3FontInfo(fontDict);
        }

        // Parse font program binary for glyph names, CIDs, and widths
        parseFontProgramData(fontDict, fontDescriptor, fontInfo);

        // Detect unmappable character codes
        detectUnmappableCharCodes(fontDict, fontInfo);

        return fontInfo;
    }

    private static bool checkIsEmbedded(PdfDictionary fontDescriptor)
    {
        if (fontDescriptor == null) return false;
        return fontDescriptor.Get(new PdfName("FontFile")) != null
            || fontDescriptor.Get(new PdfName("FontFile2")) != null
            || fontDescriptor.Get(new PdfName("FontFile3")) != null;
    }

    private static bool checkIsSymbolic(PdfDictionary fontDescriptor)
    {
        if (fontDescriptor == null) return false;
        var flags = fontDescriptor.GetAsNumber(PdfName.Flags);
        if (flags == null) return false;
        return (flags.IntValue() & (1 << 2)) != 0;
    }

    private static string readFontEncoding(PdfDictionary fontDict)
    {
        var encodingObj = fontDict.Get(PdfName.Encoding);
        if (encodingObj == null) return null;

        if (encodingObj is PdfName encodingName)
            return encodingName.GetValue();

        if (encodingObj is PdfDictionary encodingDict)
        {
            var baseEncoding = encodingDict.GetAsName(new PdfName("BaseEncoding"));
            return baseEncoding?.GetValue() ?? "Dictionary";
        }

        return encodingObj.ToString();
    }

    private static EncodingDetailData readEncodingDetail(PdfDictionary fontDict)
    {
        var encodingObj = fontDict.Get(PdfName.Encoding);
        if (encodingObj == null) return null;

        if (encodingObj is PdfName)
            return null;

        if (encodingObj is PdfDictionary encodingDict)
        {
            var baseEncoding = encodingDict.GetAsName(new PdfName("BaseEncoding"))?.GetValue();
            var differencesArr = encodingDict.GetAsArray(new PdfName("Differences"));
            var differencesGlyphNames = new List<string>();

            if (differencesArr != null)
            {
                for (int i = 0; i < differencesArr.Size(); i++)
                {
                    var item = differencesArr.Get(i);
                    if (item is PdfName glyphName)
                        differencesGlyphNames.Add(glyphName.GetValue());
                }
            }

            return new EncodingDetailData
            {
                IsDictionary = true,
                BaseEncoding = baseEncoding,
                HasDifferencesArray = differencesArr != null,
                DifferencesGlyphNames = differencesGlyphNames
            };
        }

        return null;
    }

    private static Dictionary<string, string> parseTounicode(PdfDictionary fontDict)
    {
        var mappings = new Dictionary<string, string>();
        var toUnicodeObj = fontDict.Get(PdfName.ToUnicode);
        if (toUnicodeObj == null) return mappings;

        PdfStream toUnicodeStream = null;
        if (toUnicodeObj is PdfStream stream)
        {
            toUnicodeStream = stream;
        }
        else if (toUnicodeObj is PdfIndirectReference indRef)
        {
            var refersTo = indRef.GetRefersTo();
            if (refersTo is PdfStream refStream)
                toUnicodeStream = refStream;
        }

        if (toUnicodeStream == null) return mappings;

        try
        {
            var bytes = toUnicodeStream.GetBytes();
            if (bytes == null || bytes.Length == 0) return mappings;

            var cmapContent = System.Text.Encoding.UTF8.GetString(bytes);
            parseCharMappings(cmapContent, mappings);
            parseRangeMappings(cmapContent, mappings);
        }
        catch
        {
            // Ignore parse errors
        }

        return mappings;
    }

    private static void parseCharMappings(string cmapContent, Dictionary<string, string> mappings)
    {
        var charPattern = new Regex(@"beginbfchar\s*(.*?)\s*endbfchar", RegexOptions.Singleline);
        var charMatches = charPattern.Matches(cmapContent);
        var entryPattern = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>");

        foreach (Match charMatch in charMatches)
        {
            var entries = entryPattern.Matches(charMatch.Groups[1].Value);
            foreach (Match entry in entries)
            {
                var srcHex = entry.Groups[1].Value;
                var dstHex = entry.Groups[2].Value;
                mappings[srcHex.ToUpperInvariant()] = hexToUnicodeString(dstHex);
            }
        }
    }

    private static void parseRangeMappings(string cmapContent, Dictionary<string, string> mappings)
    {
        var rangePattern = new Regex(@"beginbfrange\s*(.*?)\s*endbfrange", RegexOptions.Singleline);
        var rangeMatches = rangePattern.Matches(cmapContent);
        var entryPattern = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>");

        foreach (Match rangeMatch in rangeMatches)
        {
            var entries = entryPattern.Matches(rangeMatch.Groups[1].Value);
            foreach (Match entry in entries)
            {
                var startHex = entry.Groups[1].Value;
                var endHex = entry.Groups[2].Value;
                var dstHex = entry.Groups[3].Value;

                int start = Convert.ToInt32(startHex, 16);
                int end = Convert.ToInt32(endHex, 16);
                int dst = Convert.ToInt32(dstHex, 16);

                for (int code = start; code <= end; code++)
                {
                    var srcKey = code.ToString("X").PadLeft(startHex.Length, '0');
                    mappings[srcKey] = char.ConvertFromUtf32(dst + (code - start));
                }
            }
        }
    }

    private static string hexToUnicodeString(string hex)
    {
        if (hex.Length <= 4)
        {
            int codePoint = Convert.ToInt32(hex, 16);
            return char.ConvertFromUtf32(codePoint);
        }

        var sb = new StringBuilder();
        for (int i = 0; i < hex.Length; i += 4)
        {
            var chunk = hex.Substring(i, Math.Min(4, hex.Length - i));
            int codePoint = Convert.ToInt32(chunk, 16);
            sb.Append(char.ConvertFromUtf32(codePoint));
        }
        return sb.ToString();
    }

    private static void readType0FontInfo(PdfDictionary fontDict, FontInfo fontInfo)
    {
        var descendantFonts = fontDict.GetAsArray(new PdfName("DescendantFonts"));
        if (descendantFonts == null || descendantFonts.Size() == 0) return;

        var cidFont = descendantFonts.GetAsDictionary(0);
        if (cidFont == null) return;

        var cidFontSubtype = cidFont.GetAsName(PdfName.Subtype)?.GetValue();
        if (cidFontSubtype != null)
        {
            fontInfo.FontType = cidFontSubtype;
        }

        var cidSystemInfo = cidFont.GetAsDictionary(new PdfName("CIDSystemInfo"));
        if (cidSystemInfo != null)
        {
            fontInfo.CidSystemInfo = new CidSystemInfoData
            {
                Registry = cidSystemInfo.GetAsString(new PdfName("Registry"))?.GetValue(),
                Ordering = cidSystemInfo.GetAsString(new PdfName("Ordering"))?.GetValue(),
                Supplement = cidSystemInfo.GetAsNumber(new PdfName("Supplement"))?.IntValue() ?? 0
            };
        }

        var cidToGidMap = cidFont.Get(new PdfName("CIDToGIDMap"));
        if (cidToGidMap != null)
        {
            fontInfo.CidToGidMap = new CidToGidMapData
            {
                Present = true,
                IsValid = cidToGidMap is PdfStream || (cidToGidMap is PdfName mapName && mapName.GetValue() == "Identity")
            };
        }

        var cidFontDescriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
        if (cidFontDescriptor != null && !fontInfo.HasFontDescriptor)
        {
            fontInfo.HasFontDescriptor = true;
            fontInfo.IsEmbedded = checkIsEmbedded(cidFontDescriptor);
            fontInfo.IsSymbolic = checkIsSymbolic(cidFontDescriptor);
            fontInfo.HasCharset = cidFontDescriptor.Get(new PdfName("CharSet")) != null;
            fontInfo.HasCidset = cidFontDescriptor.Get(new PdfName("CIDSet")) != null;
        }
    }

    private static Type3FontInfoData readType3FontInfo(PdfDictionary fontDict)
    {
        var type3Info = new Type3FontInfoData
        {
            CharProcsGlyphNames = new List<string>(),
            EncodedGlyphNames = new List<string>(),
            UsedCharCodes = new List<int>(),
            TounicodeMappings = new Dictionary<string, string>(),
            HasFontDescriptor = fontDict.GetAsDictionary(PdfName.FontDescriptor) != null
        };

        var charProcs = fontDict.GetAsDictionary(new PdfName("CharProcs"));
        if (charProcs != null)
        {
            foreach (var key in charProcs.KeySet())
            {
                type3Info.CharProcsGlyphNames.Add(key.GetValue());
            }
        }

        var encodingObj = fontDict.Get(PdfName.Encoding);
        if (encodingObj is PdfDictionary encDict)
        {
            var differences = encDict.GetAsArray(new PdfName("Differences"));
            if (differences != null)
            {
                for (int i = 0; i < differences.Size(); i++)
                {
                    var item = differences.Get(i);
                    if (item is PdfName glyphName)
                        type3Info.EncodedGlyphNames.Add(glyphName.GetValue());
                }
            }
        }

        if (fontDict.Get(PdfName.ToUnicode) != null)
        {
            type3Info.TounicodeMappings = parseTounicode(fontDict);
        }

        return type3Info;
    }

    private static void parseFontProgramData(PdfDictionary fontDict, PdfDictionary fontDescriptor, FontInfo fontInfo)
    {
        var descriptor = fontDescriptor;
        if (fontDict.GetAsName(PdfName.Subtype)?.GetValue() == "Type0")
        {
            var descendantFonts = fontDict.GetAsArray(new PdfName("DescendantFonts"));
            if (descendantFonts != null && descendantFonts.Size() > 0)
            {
                var cidFont = descendantFonts.GetAsDictionary(0);
                if (cidFont != null)
                    descriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
            }
        }

        if (descriptor == null) return;

        var fontFile2 = getFontStream(descriptor, "FontFile2");
        if (fontFile2 != null)
        {
            fontInfo.FontProgramData = compressToBase64(fontFile2);
            parseTrueTypeFontProgram(fontFile2, fontInfo);
            return;
        }

        var fontFile3 = getFontStream(descriptor, "FontFile3");
        if (fontFile3 != null)
        {
            fontInfo.FontProgramData = compressToBase64(fontFile3);
            return;
        }

        var fontFile = getFontStream(descriptor, "FontFile");
        if (fontFile != null)
        {
            fontInfo.FontProgramData = compressToBase64(fontFile);
            parseType1FontProgram(fontFile, fontInfo);
        }
    }

    private static string compressToBase64(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return Convert.ToBase64String(output.ToArray());
    }

    private static byte[] getFontStream(PdfDictionary descriptor, string key)
    {
        var obj = descriptor.Get(new PdfName(key));
        if (obj is PdfIndirectReference indRef)
            obj = indRef.GetRefersTo();
        if (obj is PdfStream stream)
        {
            try { return stream.GetBytes(); }
            catch { return null; }
        }
        return null;
    }

    private static void parseTrueTypeFontProgram(byte[] data, FontInfo fontInfo)
    {
        try
        {
            if (data == null || data.Length < 12) return;

            int numTables = readUInt16BE(data, 4);
            var tables = new Dictionary<string, (int offset, int length)>();

            for (int i = 0; i < numTables; i++)
            {
                int recOff = 12 + i * 16;
                if (recOff + 16 > data.Length) break;

                var tag = Encoding.ASCII.GetString(data, recOff, 4);
                int tblOff = (int)readUInt32BE(data, recOff + 8);
                int tblLen = (int)readUInt32BE(data, recOff + 12);
                tables[tag] = (tblOff, tblLen);
            }

            // cmap → CmapSubtables
            if (tables.TryGetValue("cmap", out var cmap))
            {
                parseCmapSubtables(data, cmap.offset, cmap.length, fontInfo);
            }
        }
        catch
        {
            // Ignore font program parsing errors
        }
    }

    private static void parseCmapSubtables(byte[] data, int offset, int length, FontInfo fontInfo)
    {
        if (offset + 4 > data.Length) return;
        int numSubtables = readUInt16BE(data, offset + 2);

        for (int i = 0; i < numSubtables; i++)
        {
            int recOff = offset + 4 + i * 8;
            if (recOff + 8 > data.Length) break;

            int platformId = readUInt16BE(data, recOff);
            int encodingId = readUInt16BE(data, recOff + 2);
            int subtableOffset = (int)readUInt32BE(data, recOff + 4);
            int absOff = offset + subtableOffset;
            int format = absOff + 2 <= data.Length ? readUInt16BE(data, absOff) : 0;

            fontInfo.CmapSubtables.Add(new CmapSubtableData
            {
                PlatformId = platformId,
                EncodingId = encodingId,
                Format = format
            });
        }
    }

    private static void parseType1FontProgram(byte[] data, FontInfo fontInfo)
    {
        // Parse Type1 font for /CharStrings glyph names
        try
        {
            if (data == null || data.Length == 0) return;
            var content = Encoding.Latin1.GetString(data);
            var charStringsMatch = Regex.Match(content, @"/CharStrings\s+\d+\s+dict");
            if (!charStringsMatch.Success) return;

            int searchStart = charStringsMatch.Index + charStringsMatch.Length;
            var glyphPattern = new Regex(@"/(\w+)\s+\d+\s+RD\s");
            var matches = glyphPattern.Matches(content, searchStart);
            foreach (Match m in matches)
            {
                fontInfo.Type1GlyphNames.Add(m.Groups[1].Value);
            }
        }
        catch
        {
            // Ignore Type1 parsing errors
        }
    }

    private static void detectUnmappableCharCodes(PdfDictionary fontDict, FontInfo fontInfo)
    {
        var subtype = fontDict.GetAsName(PdfName.Subtype)?.GetValue();

        // For Type3 fonts without ToUnicode, check if glyph names are in AGL
        if (subtype == "Type3" && !fontInfo.HasTounicode)
        {
            var aglNames = getAglGlyphNames();
            var firstChar = fontDict.GetAsNumber(PdfName.FirstChar)?.IntValue() ?? 0;
            var lastChar = fontDict.GetAsNumber(PdfName.LastChar)?.IntValue() ?? 0;
            var encodingObj = fontDict.Get(PdfName.Encoding);

            // Build char code → glyph name map from Differences
            var codeToName = new Dictionary<int, string>();
            if (encodingObj is PdfDictionary encDict)
            {
                var differences = encDict.GetAsArray(new PdfName("Differences"));
                if (differences != null)
                {
                    int currentCode = 0;
                    for (int i = 0; i < differences.Size(); i++)
                    {
                        var item = differences.Get(i);
                        if (item is PdfNumber num)
                            currentCode = num.IntValue();
                        else if (item is PdfName name)
                        {
                            codeToName[currentCode] = name.GetValue();
                            currentCode++;
                        }
                    }
                }
            }

            for (int code = firstChar; code <= lastChar; code++)
            {
                if (codeToName.TryGetValue(code, out var glyphName))
                {
                    if (!aglNames.Contains(glyphName))
                        fontInfo.UnmappableCharCodes.Add(code);
                }
                else
                {
                    // No mapping at all
                    fontInfo.UnmappableCharCodes.Add(code);
                }
            }
        }
    }

    private static int readUInt16BE(byte[] data, int offset)
    {
        return (data[offset] << 8) | data[offset + 1];
    }

    private static uint readUInt32BE(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
            | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }

    private static string[] getStandardMacGlyphNames()
    {
        return new[]
        {
            ".notdef", ".null", "nonmarkingreturn", "space", "exclam", "quotedbl",
            "numbersign", "dollar", "percent", "ampersand", "quotesingle", "parenleft",
            "parenright", "asterisk", "plus", "comma", "hyphen", "period", "slash",
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight",
            "nine", "colon", "semicolon", "less", "equal", "greater", "question",
            "at", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L",
            "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "bracketleft", "backslash", "bracketright", "asciicircum", "underscore",
            "grave", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l",
            "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "braceleft", "bar", "braceright", "asciitilde", "Adieresis", "Aring",
            "Ccedilla", "Eacute", "Ntilde", "Odieresis", "Udieresis", "aacute",
            "agrave", "acircumflex", "adieresis", "atilde", "aring", "ccedilla",
            "eacute", "egrave", "ecircumflex", "edieresis", "iacute", "igrave",
            "icircumflex", "idieresis", "ntilde", "oacute", "ograve", "ocircumflex",
            "odieresis", "otilde", "uacute", "ugrave", "ucircumflex", "udieresis",
            "dagger", "degree", "cent", "sterling", "section", "bullet", "paragraph",
            "germandbls", "registered", "copyright", "trademark", "acute", "dieresis",
            "notequal", "AE", "Oslash", "infinity", "plusminus", "lessequal",
            "greaterequal", "yen", "mu", "partialdiff", "summation", "product",
            "pi", "integral", "ordfeminine", "ordmasculine", "Omega", "ae", "oslash",
            "questiondown", "exclamdown", "logicalnot", "radical", "florin",
            "approxequal", "Delta", "guillemotleft", "guillemotright", "ellipsis",
            "nonbreakingspace", "Agrave", "Atilde", "Otilde", "OE", "oe", "endash",
            "emdash", "quotedblleft", "quotedblright", "quoteleft", "quoteright",
            "divide", "lozenge", "ydieresis", "Ydieresis", "fraction", "currency",
            "guilsinglleft", "guilsinglright", "fi", "fl", "daggerdbl", "periodcentered",
            "quotesinglbase", "quotedblbase", "perthousand", "Acircumflex",
            "Ecircumflex", "Aacute", "Edieresis", "Egrave", "Iacute", "Icircumflex",
            "Idieresis", "Igrave", "Oacute", "Ocircumflex", "apple", "Ograve",
            "Uacute", "Ucircumflex", "Ugrave", "dotlessi", "circumflex", "tilde",
            "macron", "breve", "dotaccent", "ring", "cedilla", "hungarumlaut",
            "ogonek", "caron", "Lslash", "lslash", "Scaron", "scaron", "Zcaron",
            "zcaron", "brokenbar", "Eth", "eth", "Yacute", "yacute", "Thorn",
            "thorn", "minus", "multiply", "onesuperior", "twosuperior",
            "threesuperior", "onehalf", "onequarter", "threequarters", "franc",
            "Gbreve", "gbreve", "Idotaccent", "Scedilla", "scedilla", "Cacute",
            "cacute", "Ccaron", "ccaron", "dcroat"
        };
    }

    private static HashSet<string> getAglGlyphNames()
    {
        // Common AGL (Adobe Glyph List) names that map to Unicode
        // This is a subset covering the most common glyph names
        var names = getStandardMacGlyphNames();
        var set = new HashSet<string>(names, StringComparer.Ordinal);
        // Add standard Latin glyph names that map to Unicode
        foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
            set.Add(c.ToString());
        return set;
    }

    private static List<int> readStructuredMcids(PdfDocument pdf, int pageNumber)
    {
        var mcids = new List<int>();
        if (!pdf.IsTagged()) return mcids;

        var structRoot = pdf.GetStructTreeRoot();
        if (structRoot == null) return mcids;

        collectMcidsForPage(pdf, structRoot.GetKids(), pageNumber, mcids);
        mcids.Sort();
        return mcids;
    }

    private static void collectMcidsForPage(PdfDocument pdf, IList<IStructureNode> kids, int pageNumber, List<int> mcids)
    {
        if (kids == null) return;

        foreach (var kid in kids)
        {
            if (kid is PdfMcr mcr)
            {
                var pageObj = mcr.GetPageObject();
                if (pageObj != null)
                {
                    var pn = findPageNumber(pdf, pageObj);
                    if (pn == pageNumber)
                    {
                        mcids.Add(mcr.GetMcid());
                    }
                }
            }
            else if (kid is PdfStructElem elem)
            {
                // Also check for MCIDs embedded in /K dictionary entries
                collectMcidsFromDict(pdf, elem.GetPdfObject(), pageNumber, mcids);
                collectMcidsForPage(pdf, elem.GetKids(), pageNumber, mcids);
            }
        }
    }

    private static void collectMcidsFromDict(PdfDocument pdf, PdfDictionary elemDict, int pageNumber, List<int> mcids)
    {
        var k = elemDict.Get(PdfName.K);
        if (k == null) return;

        var pgObj = elemDict.GetAsDictionary(PdfName.Pg);
        int? elemPage = pgObj != null ? findPageNumber(pdf, pgObj) : null;

        if (k is PdfNumber kNum)
        {
            if (elemPage == pageNumber)
                mcids.Add(kNum.IntValue());
        }
        else if (k is PdfDictionary kDict)
        {
            extractMcidFromMcrDict(pdf, kDict, elemPage, pageNumber, mcids);
        }
        else if (k is PdfArray kArr)
        {
            for (int i = 0; i < kArr.Size(); i++)
            {
                var item = kArr.Get(i);
                if (item is PdfNumber itemNum)
                {
                    if (elemPage == pageNumber)
                        mcids.Add(itemNum.IntValue());
                }
                else if (item is PdfDictionary itemDict)
                {
                    extractMcidFromMcrDict(pdf, itemDict, elemPage, pageNumber, mcids);
                }
            }
        }
    }

    private static void extractMcidFromMcrDict(PdfDocument pdf, PdfDictionary mcrDict, int? parentPage, int pageNumber, List<int> mcids)
    {
        var type = mcrDict.GetAsName(PdfName.Type)?.GetValue();
        if (type == "MCR" || mcrDict.ContainsKey(new PdfName("MCID")))
        {
            var mcidObj = mcrDict.GetAsNumber(new PdfName("MCID"));
            if (mcidObj == null) return;

            var mcrPg = mcrDict.GetAsDictionary(PdfName.Pg);
            int? mcrPage = mcrPg != null ? findPageNumber(pdf, mcrPg) : parentPage;
            if (mcrPage == pageNumber)
                mcids.Add(mcidObj.IntValue());
        }
    }

    private static List<OutlineNode> readOutlines(PdfDocument pdf)
    {
        var result = new List<OutlineNode>();

        var outlines = pdf.GetOutlines(false);
        if (outlines == null) return result;

        var children = outlines.GetAllChildren();
        if (children == null) return result;

        foreach (var child in children)
        {
            result.Add(buildOutlineNode(child));
        }

        return result;
    }

    private static OutlineNode buildOutlineNode(PdfOutline outline)
    {
        var node = new OutlineNode
        {
            Title = outline.GetTitle(),
            Children = new List<OutlineNode>()
        };

        // Read /Lang from the outline dictionary if present
        var outlineDict = outline.GetContent();
        if (outlineDict != null)
        {
            var lang = outlineDict.GetAsString(PdfName.Lang);
            node.Lang = lang?.GetValue();
        }

        var children = outline.GetAllChildren();
        if (children != null)
        {
            foreach (var child in children)
            {
                node.Children.Add(buildOutlineNode(child));
            }
        }

        return node;
    }

    private static List<EmbeddedFileInfo> readEmbeddedFiles(PdfDocument pdf)
    {
        var result = new List<EmbeddedFileInfo>();
        var catalog = pdf.GetCatalog().GetPdfObject();
        var names = catalog.GetAsDictionary(PdfName.Names);
        if (names == null) return result;

        var embeddedFiles = names.GetAsDictionary(new PdfName("EmbeddedFiles"));
        if (embeddedFiles == null) return result;

        var namesArray = embeddedFiles.GetAsArray(PdfName.Names);
        if (namesArray == null) return result;

        // Names array is [key1, value1, key2, value2, ...]
        for (int i = 1; i < namesArray.Size(); i += 2)
        {
            var fileSpec = namesArray.GetAsDictionary(i);
            if (fileSpec == null) continue;

            result.Add(new EmbeddedFileInfo
            {
                HasFKey = fileSpec.Get(PdfName.F) != null,
                HasUfKey = fileSpec.Get(new PdfName("UF")) != null
            });
        }

        return result;
    }

    private static List<OcgConfigInfo> readOcgConfigurations(PdfDocument pdf)
    {
        var result = new List<OcgConfigInfo>();
        var catalog = pdf.GetCatalog().GetPdfObject();
        var ocProperties = catalog.GetAsDictionary(new PdfName("OCProperties"));
        if (ocProperties == null) return result;

        // Read default config (D)
        var defaultConfig = ocProperties.GetAsDictionary(new PdfName("D"));
        if (defaultConfig != null)
        {
            result.Add(buildOcgConfigInfo(defaultConfig));
        }

        // Read additional configs (Configs)
        var configs = ocProperties.GetAsArray(new PdfName("Configs"));
        if (configs != null)
        {
            for (int i = 0; i < configs.Size(); i++)
            {
                var config = configs.GetAsDictionary(i);
                if (config != null)
                {
                    result.Add(buildOcgConfigInfo(config));
                }
            }
        }

        return result;
    }

    private static OcgConfigInfo buildOcgConfigInfo(PdfDictionary config)
    {
        var name = config.GetAsString(PdfName.Name)?.GetValue();
        var hasAs = config.Get(new PdfName("AS")) != null;
        var intent = config.GetAsName(new PdfName("Intent"))?.GetValue();

        // Intent can also be an array
        if (intent == null)
        {
            var intentArr = config.GetAsArray(new PdfName("Intent"));
            if (intentArr != null && intentArr.Size() > 0)
            {
                var firstIntent = intentArr.GetAsName(0);
                intent = firstIntent?.GetValue();
            }
        }

        return new OcgConfigInfo
        {
            Name = name,
            HasAsKey = hasAs,
            Intent = intent
        };
    }

    public byte[] GetPageImage(Stream pdfStream, int page, int index)
    {
        // Stub — returns null until fully implemented
        return null;
    }

    public FigureInfoResponse GetFigureInfo(Stream pdfStream, int page, int mcid)
    {
        // Stub — returns null until fully implemented
        return null;
    }
}
