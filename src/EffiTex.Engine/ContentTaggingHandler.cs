using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;

namespace EffiTex.Engine;

public class ContentTaggingHandler
{
    private readonly BboxResolver _resolver;

    public ContentTaggingHandler(BboxResolver resolver)
    {
        _resolver = resolver;
    }

    public void Apply(PdfDocument pdf, List<ContentTaggingEntry> entries, Dictionary<string, PdfStructElem> nodeIndex)
    {
        if (entries == null || entries.Count == 0) return;

        // Group entries by page to process each page once
        var byPage = new Dictionary<int, List<ContentTaggingEntry>>();
        foreach (var entry in entries)
        {
            if (!byPage.TryGetValue(entry.Page, out var list))
            {
                list = new List<ContentTaggingEntry>();
                byPage[entry.Page] = list;
            }
            list.Add(entry);
        }

        foreach (var kvp in byPage)
        {
            int pageNum = kvp.Key;
            var pageEntries = kvp.Value;
            var page = pdf.GetPage(pageNum);

            // Resolve all entries for this page and pair them with their operator indices
            var resolvedEntries = new List<(ContentTaggingEntry Entry, List<int> Indices)>();
            foreach (var entry in pageEntries)
            {
                var indices = _resolver.Resolve(page, entry.Bbox);
                if (indices.Count > 0)
                {
                    resolvedEntries.Add((entry, indices));
                }
            }

            if (resolvedEntries.Count == 0) continue;

            // Collect the existing content stream bytes
            var contentBytes = extractPageContentBytes(page);
            var operators = parseContentOperators(contentBytes);

            // Build new content stream with BDC/EMC markers
            var newContent = buildTaggedContent(pdf, operators, resolvedEntries, nodeIndex, page);

            // Replace page content
            replacePageContent(page, newContent);
        }
    }

    private static byte[] extractPageContentBytes(PdfPage page)
    {
        var contentStream = page.GetContentStream(0);
        if (contentStream == null)
            return Array.Empty<byte>();

        // Combine all content streams
        var allBytes = new List<byte>();
        int streamCount = page.GetContentStreamCount();
        for (int i = 0; i < streamCount; i++)
        {
            var stream = page.GetContentStream(i);
            if (stream != null)
            {
                var bytes = stream.GetBytes();
                if (bytes != null)
                {
                    allBytes.AddRange(bytes);
                    allBytes.Add((byte)'\n');
                }
            }
        }

        return allBytes.ToArray();
    }

    private static List<ContentOperator> parseContentOperators(byte[] contentBytes)
    {
        var result = new List<ContentOperator>();
        var text = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // We track operator index based on text-rendering operators (BT...ET blocks with Tj/TJ)
        // and image operators (Do). This matches BboxResolver's indexing.
        int operatorIndex = 0;
        bool inTextBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line == "BT")
            {
                inTextBlock = true;
                result.Add(new ContentOperator { Line = line, OperatorIndex = -1 });
            }
            else if (line == "ET")
            {
                inTextBlock = false;
                result.Add(new ContentOperator { Line = line, OperatorIndex = -1 });
            }
            else if (inTextBlock && (line.EndsWith(" Tj") || line.EndsWith(" TJ") || line.EndsWith("Tj") || line.EndsWith("TJ")))
            {
                result.Add(new ContentOperator { Line = line, OperatorIndex = operatorIndex });
                operatorIndex++;
            }
            else if (line.EndsWith(" Do"))
            {
                result.Add(new ContentOperator { Line = line, OperatorIndex = operatorIndex });
                operatorIndex++;
            }
            else
            {
                result.Add(new ContentOperator { Line = line, OperatorIndex = -1 });
            }
        }

        return result;
    }

    private static string buildTaggedContent(PdfDocument pdf, List<ContentOperator> operators,
        List<(ContentTaggingEntry Entry, List<int> Indices)> resolvedEntries,
        Dictionary<string, PdfStructElem> nodeIndex, PdfPage page)
    {
        // Build a mapping from operator index to the MCID it should be tagged with
        var operatorToMcid = new Dictionary<int, int>();
        var mcidToStructElem = new Dictionary<int, PdfStructElem>();

        var structTreeRoot = pdf.GetStructTreeRoot();
        int nextMcid = 0;

        foreach (var (entry, indices) in resolvedEntries)
        {
            if (!nodeIndex.TryGetValue(entry.Node, out var structElem))
                continue;

            int mcid = nextMcid++;
            mcidToStructElem[mcid] = structElem;

            foreach (var idx in indices)
            {
                if (!operatorToMcid.ContainsKey(idx))
                {
                    operatorToMcid[idx] = mcid;
                }
            }
        }

        // Build new content, wrapping tagged operators in BDC/EMC
        var sb = new System.Text.StringBuilder();
        var activeMcid = -1;
        bool needsClosingEmc = false;

        foreach (var op in operators)
        {
            if (op.Line == "BT")
            {
                sb.AppendLine(op.Line);
                continue;
            }

            if (op.Line == "ET")
            {
                if (needsClosingEmc)
                {
                    sb.AppendLine("EMC");
                    needsClosingEmc = false;
                    activeMcid = -1;
                }
                sb.AppendLine(op.Line);
                continue;
            }

            if (op.OperatorIndex >= 0 && operatorToMcid.TryGetValue(op.OperatorIndex, out int mcid))
            {
                if (activeMcid != mcid)
                {
                    if (needsClosingEmc)
                    {
                        sb.AppendLine("EMC");
                    }
                    sb.AppendLine($"/P <</MCID {mcid}>> BDC");
                    activeMcid = mcid;
                    needsClosingEmc = true;
                }
                sb.AppendLine(op.Line);
            }
            else
            {
                if (needsClosingEmc)
                {
                    sb.AppendLine("EMC");
                    needsClosingEmc = false;
                    activeMcid = -1;
                }
                sb.AppendLine(op.Line);
            }
        }

        if (needsClosingEmc)
        {
            sb.AppendLine("EMC");
        }

        // Create MCR entries in structure elements and update parent tree
        var parentTreeNextKey = structTreeRoot.GetPdfObject()
            .GetAsNumber(new PdfName("ParentTreeNextKey"));
        int nextKey = parentTreeNextKey?.IntValue() ?? 0;

        var parentTree = structTreeRoot.GetPdfObject().GetAsDictionary(new PdfName("ParentTree"));
        if (parentTree == null)
        {
            parentTree = new PdfDictionary();
            parentTree.Put(PdfName.Type, new PdfName("NumberTree"));
            var nums = new PdfArray();
            parentTree.Put(new PdfName("Nums"), nums);
            parentTree.MakeIndirect(pdf);
            structTreeRoot.GetPdfObject().Put(new PdfName("ParentTree"), parentTree);
        }

        var numsArray = parentTree.GetAsArray(new PdfName("Nums"));
        if (numsArray == null)
        {
            numsArray = new PdfArray();
            parentTree.Put(new PdfName("Nums"), numsArray);
        }

        // For each MCID, create a marked content reference
        var parentArray = new PdfArray();
        foreach (var kvp in mcidToStructElem)
        {
            int mcidVal = kvp.Key;
            var structElem = kvp.Value;

            var mcr = new PdfDictionary();
            mcr.Put(PdfName.Type, new PdfName("MCR"));
            mcr.Put(PdfName.Pg, page.GetPdfObject());
            mcr.Put(new PdfName("MCID"), new PdfNumber(mcidVal));
            mcr.MakeIndirect(pdf);

            var elemDict = structElem.GetPdfObject();
            var existingK = elemDict.Get(PdfName.K);
            if (existingK == null)
            {
                elemDict.Put(PdfName.K, mcr);
            }
            else if (existingK is PdfArray kArray)
            {
                kArray.Add(mcr);
            }
            else
            {
                var newArray = new PdfArray();
                newArray.Add(existingK);
                newArray.Add(mcr);
                elemDict.Put(PdfName.K, newArray);
            }

            // Add to parent array for parent tree
            while (parentArray.Size() <= mcidVal)
            {
                parentArray.Add(PdfNull.PDF_NULL);
            }
            parentArray.Set(mcidVal, structElem.GetPdfObject());
        }

        // Set StructParents on the page
        page.GetPdfObject().Put(new PdfName("StructParents"), new PdfNumber(nextKey));

        // Add parent tree entry
        numsArray.Add(new PdfNumber(nextKey));
        numsArray.Add(parentArray);

        structTreeRoot.GetPdfObject()
            .Put(new PdfName("ParentTreeNextKey"), new PdfNumber(nextKey + 1));

        return sb.ToString();
    }

    private static void replacePageContent(PdfPage page, string newContent)
    {
        var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(newContent);

        // Clear existing content streams and write new content into the first one
        int streamCount = page.GetContentStreamCount();
        if (streamCount > 0)
        {
            // Overwrite the first content stream with our tagged content
            var firstStream = page.GetContentStream(0);
            firstStream.SetData(bytes);

            // Clear any additional content streams
            for (int i = 1; i < streamCount; i++)
            {
                var stream = page.GetContentStream(i);
                stream.SetData(Array.Empty<byte>());
            }
        }
        else
        {
            var newStream = page.NewContentStreamBefore();
            newStream.SetData(bytes);
        }
    }

    private class ContentOperator
    {
        public string Line { get; set; }
        public int OperatorIndex { get; set; }
    }
}
