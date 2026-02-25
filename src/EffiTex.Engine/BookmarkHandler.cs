using System.Text;
using System.Text.RegularExpressions;
using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Pdf.Tagging;

namespace EffiTex.Engine;

public class BookmarkHandler
{
    private static readonly HashSet<string> HeadingRoles = new(StringComparer.Ordinal)
    {
        "H1", "H2", "H3", "H4", "H5", "H6"
    };

    public void Apply(PdfDocument pdf, BookmarksInstruction instruction)
    {
        if (instruction == null || !instruction.GenerateFromHeadings) return;

        var structRoot = pdf.GetStructTreeRoot();
        if (structRoot == null) return;

        var mcidTextCache = new Dictionary<int, Dictionary<int, string>>();
        var headings = new List<HeadingInfo>();
        CollectHeadings(pdf, structRoot.GetKids(), headings, mcidTextCache);

        // Fallback: if MCR-based approach found no headings, try content stream scanning
        if (headings.Count == 0)
        {
            CollectHeadingsFromContentStreams(pdf, structRoot, headings);
        }

        if (headings.Count == 0) return;

        var outlineRoot = pdf.GetOutlines(true);
        BuildOutlineTree(pdf, outlineRoot, headings);
    }

    private static void CollectHeadings(PdfDocument pdf, IList<IStructureNode> kids,
        List<HeadingInfo> headings, Dictionary<int, Dictionary<int, string>> mcidTextCache)
    {
        if (kids == null) return;

        foreach (var kid in kids)
        {
            if (kid is PdfStructElem elem)
            {
                var role = elem.GetRole()?.GetValue();
                if (role != null && HeadingRoles.Contains(role))
                {
                    int level = role[1] - '0';
                    var (text, pageNum) = ExtractTextAndPageViaMcr(pdf, elem, mcidTextCache);

                    if (string.IsNullOrEmpty(text) || pageNum <= 0)
                    {
                        // Try direct dictionary access for /K and /Pg
                        (text, pageNum) = ExtractTextAndPageViaDictionary(pdf, elem, mcidTextCache);
                    }

                    if (!string.IsNullOrEmpty(text) && pageNum > 0)
                    {
                        headings.Add(new HeadingInfo { Level = level, Title = text, PageNumber = pageNum });
                    }
                }

                CollectHeadings(pdf, elem.GetKids(), headings, mcidTextCache);
            }
        }
    }

    private static (string text, int pageNumber) ExtractTextAndPageViaMcr(PdfDocument pdf, PdfStructElem elem,
        Dictionary<int, Dictionary<int, string>> mcidTextCache)
    {
        var actualText = elem.GetPdfObject().GetAsString(new PdfName("ActualText"));
        if (actualText != null)
        {
            int pg = FindPageForElementViaMcr(pdf, elem);
            if (pg > 0) return (actualText.GetValue(), pg);
        }

        var kids = elem.GetKids();
        if (kids == null) return (null, -1);

        var sb = new StringBuilder();
        int pageNum = -1;

        foreach (var kid in kids)
        {
            if (kid is PdfMcr mcr)
            {
                int mcid = mcr.GetMcid();
                var pageObj = mcr.GetPageObject();
                if (pageObj == null) continue;

                int pn = FindPageNumber(pdf, pageObj);
                if (pn <= 0) continue;

                if (pageNum < 0) pageNum = pn;

                if (!mcidTextCache.TryGetValue(pn, out var pageMap))
                {
                    pageMap = BuildMcidTextMap(pdf.GetPage(pn));
                    mcidTextCache[pn] = pageMap;
                }

                if (pageMap.TryGetValue(mcid, out var text))
                {
                    sb.Append(text);
                }
            }
        }

        return (sb.Length > 0 ? sb.ToString().Trim() : null, pageNum);
    }

    private static (string text, int pageNumber) ExtractTextAndPageViaDictionary(PdfDocument pdf, PdfStructElem elem,
        Dictionary<int, Dictionary<int, string>> mcidTextCache)
    {
        var elemDict = elem.GetPdfObject();

        // Get page from /Pg entry
        var pgObj = elemDict.GetAsDictionary(PdfName.Pg);
        if (pgObj == null) return (null, -1);

        int pageNum = FindPageNumber(pdf, pgObj);
        if (pageNum <= 0) return (null, -1);

        // Get MCID from /K entry
        var k = elemDict.Get(PdfName.K);
        int mcid = -1;

        if (k is PdfNumber kNum)
        {
            mcid = kNum.IntValue();
        }
        else if (k is PdfDictionary kDict)
        {
            var mcidObj = kDict.GetAsNumber(new PdfName("MCID"));
            if (mcidObj != null) mcid = mcidObj.IntValue();
        }
        else if (k is PdfArray kArr && kArr.Size() > 0)
        {
            var first = kArr.Get(0);
            if (first is PdfNumber firstNum) mcid = firstNum.IntValue();
            else if (first is PdfDictionary firstDict)
            {
                var mcidObj = firstDict.GetAsNumber(new PdfName("MCID"));
                if (mcidObj != null) mcid = mcidObj.IntValue();
            }
        }

        if (mcid < 0) return (null, pageNum);

        // Check for ActualText
        var actualText = elemDict.GetAsString(new PdfName("ActualText"));
        if (actualText != null) return (actualText.GetValue(), pageNum);

        // Extract text from content stream
        if (!mcidTextCache.TryGetValue(pageNum, out var pageMap))
        {
            pageMap = BuildMcidTextMap(pdf.GetPage(pageNum));
            mcidTextCache[pageNum] = pageMap;
        }

        if (pageMap.TryGetValue(mcid, out var text))
        {
            return (text, pageNum);
        }

        return (null, pageNum);
    }

    private static int FindPageForElementViaMcr(PdfDocument pdf, PdfStructElem elem)
    {
        var kids = elem.GetKids();
        if (kids == null) return -1;

        foreach (var kid in kids)
        {
            if (kid is PdfMcr mcr)
            {
                var pageObj = mcr.GetPageObject();
                if (pageObj != null)
                    return FindPageNumber(pdf, pageObj);
            }
        }
        return -1;
    }

    private static int FindPageNumber(PdfDocument pdf, PdfDictionary pageObj)
    {
        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            if (pdf.GetPage(i).GetPdfObject() == pageObj)
                return i;
        }
        return -1;
    }

    private static void CollectHeadingsFromContentStreams(PdfDocument pdf, PdfStructTreeRoot structRoot,
        List<HeadingInfo> headings)
    {
        // Walk struct tree for heading roles and titles
        var structHeadings = new List<(int Level, string Role, string Title)>();
        CollectStructHeadingInfo(structRoot.GetKids(), structHeadings);

        if (structHeadings.Count == 0) return;

        // Scan content streams for heading BDC blocks
        var contentHeadings = new List<(string Role, string Text, int PageNumber)>();
        for (int p = 1; p <= pdf.GetNumberOfPages(); p++)
        {
            var page = pdf.GetPage(p);
            var contentBytes = page.GetContentBytes();
            if (contentBytes == null || contentBytes.Length == 0) continue;

            var content = Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);
            var bdcPattern = new Regex(@"/(\w+)\s+(?:<<[^>]*>>\s*BDC|(\d+)\s*BDC)");
            var matches = bdcPattern.Matches(content);

            foreach (Match match in matches)
            {
                var role = match.Groups[1].Value;
                if (!HeadingRoles.Contains(role)) continue;

                int startPos = match.Index + match.Length;
                int emcPos = content.IndexOf("EMC", startPos, StringComparison.Ordinal);
                if (emcPos < 0) continue;

                string block = content.Substring(startPos, emcPos - startPos);
                string text = ExtractTextFromBlock(block);

                if (!string.IsNullOrEmpty(text))
                {
                    contentHeadings.Add((role, text, p));
                }
            }
        }

        // Match by role order
        for (int i = 0; i < structHeadings.Count && i < contentHeadings.Count; i++)
        {
            var sh = structHeadings[i];
            var ch = contentHeadings[i];

            if (string.Equals(sh.Role, ch.Role, StringComparison.Ordinal))
            {
                string title = sh.Title != sh.Role ? sh.Title : ch.Text;
                headings.Add(new HeadingInfo { Level = sh.Level, Title = title, PageNumber = ch.PageNumber });
            }
        }
    }

    private static void CollectStructHeadingInfo(IList<IStructureNode> kids,
        List<(int Level, string Role, string Title)> headings)
    {
        if (kids == null) return;

        foreach (var kid in kids)
        {
            if (kid is PdfStructElem elem)
            {
                var role = elem.GetRole()?.GetValue();
                if (role != null && HeadingRoles.Contains(role))
                {
                    int level = role[1] - '0';
                    var actualText = elem.GetPdfObject().GetAsString(new PdfName("ActualText"))?.GetValue();
                    var alt = elem.GetPdfObject().GetAsString(PdfName.Alt)?.GetValue();
                    string title = actualText ?? alt ?? role;
                    headings.Add((level, role, title));
                }

                CollectStructHeadingInfo(elem.GetKids(), headings);
            }
        }
    }

    private static string ExtractTextFromBlock(string block)
    {
        var sb = new StringBuilder();
        var tjPattern = new Regex(@"\(([^)]*)\)\s*Tj");

        foreach (Match tm in tjPattern.Matches(block))
        {
            sb.Append(UnescapePdfString(tm.Groups[1].Value));
        }

        var tjArrayPattern = new Regex(@"\[([^\]]*)\]\s*TJ");
        foreach (Match tam in tjArrayPattern.Matches(block))
        {
            var innerPattern = new Regex(@"\(([^)]*)\)");
            foreach (Match im in innerPattern.Matches(tam.Groups[1].Value))
            {
                sb.Append(UnescapePdfString(im.Groups[1].Value));
            }
        }

        return sb.ToString();
    }

    private static Dictionary<int, string> BuildMcidTextMap(PdfPage page)
    {
        var map = new Dictionary<int, string>();
        var contentBytes = page.GetContentBytes();
        if (contentBytes == null || contentBytes.Length == 0) return map;

        var content = Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);

        var bdcPattern = new Regex(@"/\w+\s+(?:<<[^>]*/MCID\s+(\d+)[^>]*>>|(\d+))\s*BDC");

        var matches = bdcPattern.Matches(content);
        foreach (Match match in matches)
        {
            int mcid = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);

            int startPos = match.Index + match.Length;
            int emcPos = content.IndexOf("EMC", startPos, StringComparison.Ordinal);
            if (emcPos < 0) continue;

            string block = content.Substring(startPos, emcPos - startPos);
            string text = ExtractTextFromBlock(block);

            if (!string.IsNullOrEmpty(text))
                map[mcid] = text;
        }

        return map;
    }

    private static string UnescapePdfString(string s)
    {
        return s.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\");
    }

    private static void BuildOutlineTree(PdfDocument pdf, PdfOutline outlineRoot, List<HeadingInfo> headings)
    {
        var stack = new Stack<(int Level, PdfOutline Outline)>();
        stack.Push((0, outlineRoot));

        foreach (var heading in headings)
        {
            while (stack.Count > 1 && stack.Peek().Level >= heading.Level)
                stack.Pop();

            var parent = stack.Peek().Outline;
            var page = pdf.GetPage(heading.PageNumber);
            var outline = parent.AddOutline(heading.Title);
            outline.AddDestination(PdfExplicitDestination.CreateFit(page));

            stack.Push((heading.Level, outline));
        }
    }

    private class HeadingInfo
    {
        public int Level;
        public string Title;
        public int PageNumber;
    }
}
