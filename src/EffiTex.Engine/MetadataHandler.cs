using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.XMP;

namespace EffiTex.Engine;

public class MetadataHandler
{
    private static readonly Dictionary<string, string> TabOrderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "structure", "S" },
        { "row", "R" },
        { "column", "C" },
        { "unordered", "W" }
    };

    public void Apply(PdfDocument pdf, MetadataInstruction instruction)
    {
        if (instruction == null)
            return;

        var catalog = pdf.GetCatalog().GetPdfObject();
        XMPMeta xmp = null;

        // Language
        if (!string.IsNullOrEmpty(instruction.Language))
        {
            pdf.GetCatalog().SetLang(new PdfString(instruction.Language));
        }

        // Title
        if (!string.IsNullOrEmpty(instruction.Title))
        {
            pdf.GetDocumentInfo().SetTitle(instruction.Title);
            xmp = GetOrCreateXmp(pdf);
            xmp.SetLocalizedText(
                "http://purl.org/dc/elements/1.1/",
                "title",
                "",
                "x-default",
                instruction.Title);
        }

        // DisplayDocTitle
        if (instruction.DisplayDocTitle == true)
        {
            var vp = catalog.GetAsDictionary(PdfName.ViewerPreferences) ?? new PdfDictionary();
            vp.Put(new PdfName("DisplayDocTitle"), new PdfBoolean(true));
            catalog.Put(PdfName.ViewerPreferences, vp);
        }

        // MarkInfo
        if (instruction.MarkInfo == true)
        {
            var markInfo = new PdfDictionary();
            markInfo.Put(PdfName.Marked, new PdfBoolean(true));
            markInfo.Put(new PdfName("Suspect"), new PdfBoolean(false));
            catalog.Put(PdfName.MarkInfo, markInfo);
        }

        // PdfUaIdentifier
        if (instruction.PdfUaIdentifier.HasValue)
        {
            xmp ??= GetOrCreateXmp(pdf);
            xmp.SetPropertyInteger(
                "http://www.aiim.org/pdfua/ns/id/",
                "part",
                instruction.PdfUaIdentifier.Value);
        }

        // Write XMP if modified — SetXmpMetadata accepts XMPMeta in iText 9.x
        if (xmp != null)
        {
            pdf.SetXmpMetadata(xmp);
        }

        // Tab order
        if (!string.IsNullOrEmpty(instruction.TabOrder) &&
            TabOrderMap.TryGetValue(instruction.TabOrder, out var tabValue))
        {
            for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
            {
                pdf.GetPage(i).GetPdfObject().Put(PdfName.Tabs, new PdfName(tabValue));
            }
        }
    }

    private XMPMeta GetOrCreateXmp(PdfDocument pdf)
    {
        // GetXmpMetadata(false) returns XMPMeta directly in iText 9
        var existing = pdf.GetXmpMetadata(false);
        if (existing != null)
            return existing;
        return XMPMetaFactory.Create();
    }
}
