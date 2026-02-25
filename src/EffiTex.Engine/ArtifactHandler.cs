using EffiTex.Core.Models;
using iText.Kernel.Pdf;

namespace EffiTex.Engine;

public class ArtifactHandler
{
    private static readonly Dictionary<string, string> ARTIFACT_TYPE_MAP = new(StringComparer.OrdinalIgnoreCase)
    {
        { "layout", "Layout" },
        { "header", "Header" },
        { "footer", "Footer" },
        { "pagination", "Pagination" },
        { "background", "Background" }
    };

    private readonly BboxResolver _resolver;

    public ArtifactHandler(BboxResolver resolver)
    {
        _resolver = resolver;
    }

    public void Apply(PdfDocument pdf, List<ArtifactEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;

        var byPage = new Dictionary<int, List<ArtifactEntry>>();
        foreach (var entry in entries)
        {
            if (!byPage.TryGetValue(entry.Page, out var list))
            {
                list = new List<ArtifactEntry>();
                byPage[entry.Page] = list;
            }
            list.Add(entry);
        }

        foreach (var kvp in byPage)
        {
            int pageNum = kvp.Key;
            var pageEntries = kvp.Value;
            var page = pdf.GetPage(pageNum);

            var resolvedEntries = new List<(ArtifactEntry Entry, List<int> Indices)>();
            foreach (var entry in pageEntries)
            {
                var indices = _resolver.Resolve(page, entry.Bbox);
                if (indices.Count > 0)
                {
                    resolvedEntries.Add((entry, indices));
                }
            }

            if (resolvedEntries.Count == 0) continue;

            var contentBytes = page.GetContentBytes();
            if (contentBytes == null || contentBytes.Length == 0) continue;

            var content = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(contentBytes);
            var operators = parseContentOperators(content);
            var newContent = buildArtifactContent(operators, resolvedEntries);

            replacePageContent(page, newContent);
        }
    }

    private static List<ContentOperator> parseContentOperators(string content)
    {
        var result = new List<ContentOperator>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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

    private static string buildArtifactContent(List<ContentOperator> operators,
        List<(ArtifactEntry Entry, List<int> Indices)> resolvedEntries)
    {
        var operatorToType = new Dictionary<int, string>();

        foreach (var (entry, indices) in resolvedEntries)
        {
            string pdfType = ARTIFACT_TYPE_MAP.TryGetValue(entry.Type ?? "", out var mapped) ? mapped : "Layout";

            foreach (var idx in indices)
            {
                if (!operatorToType.ContainsKey(idx))
                {
                    operatorToType[idx] = pdfType;
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        string activeType = null;
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
                    activeType = null;
                }
                sb.AppendLine(op.Line);
                continue;
            }

            if (op.OperatorIndex >= 0 && operatorToType.TryGetValue(op.OperatorIndex, out var type))
            {
                if (activeType != type)
                {
                    if (needsClosingEmc)
                    {
                        sb.AppendLine("EMC");
                    }
                    sb.AppendLine($"/Artifact <</Type /{type}>> BDC");
                    activeType = type;
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
                    activeType = null;
                }
                sb.AppendLine(op.Line);
            }
        }

        if (needsClosingEmc)
        {
            sb.AppendLine("EMC");
        }

        return sb.ToString();
    }

    private static void replacePageContent(PdfPage page, string newContent)
    {
        var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(newContent);

        int streamCount = page.GetContentStreamCount();
        if (streamCount > 0)
        {
            var firstStream = page.GetContentStream(0);
            firstStream.SetData(bytes);

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
