using System.Security.Cryptography;
using System.Text;
using EffiTex.Engine.Models.Inspect;
using iText.Kernel.Pdf;
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

        var response = new InspectResponse
        {
            FileHash = fileHash,
            FileSizeBytes = streamBytes.Length,
            Document = buildDocumentInfo(pdf),
            XmpMetadata = readXmpMetadata(pdf),
            StructureTree = readStructureTree(pdf),
            RoleMap = readRoleMap(pdf),
            Pages = readPages(pdf),
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
        stream.Position = 0;
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

    private static List<PageInfo> readPages(PdfDocument pdf)
    {
        var pages = new List<PageInfo>();

        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            var page = pdf.GetPage(i);
            var pageDict = page.GetPdfObject();
            var mediaBox = page.GetMediaBox();

            var tabOrder = pageDict.GetAsName(PdfName.Tabs)?.GetValue();

            var pageInfo = new PageInfo
            {
                PageNumber = i,
                Width = mediaBox.GetWidth(),
                Height = mediaBox.GetHeight(),
                TabOrder = tabOrder,
                StructuredMcids = readStructuredMcids(pdf, i)
            };

            pages.Add(pageInfo);
        }

        return pages;
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
}
