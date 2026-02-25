using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;

namespace EffiTex.Engine;

public class StructureHandler
{
    public Dictionary<string, PdfStructElem> Apply(PdfDocument pdf, StructureInstruction instruction)
    {
        if (instruction == null)
            return new Dictionary<string, PdfStructElem>();

        // Ensure the document is tagged
        if (!pdf.IsTagged())
            pdf.SetTagged();

        var structRoot = pdf.GetStructTreeRoot();

        // Strip existing structure tree if requested
        if (instruction.StripExisting)
        {
            StripExistingStructure(pdf);
        }

        var index = new Dictionary<string, PdfStructElem>();

        // Create root structure element
        if (!string.IsNullOrEmpty(instruction.Root))
        {
            var rootElem = new PdfStructElem(pdf, new PdfName(instruction.Root));
            structRoot.AddKid(rootElem);

            // Recursively create children
            if (instruction.Children != null)
            {
                foreach (var child in instruction.Children)
                {
                    CreateNode(pdf, rootElem, child, index);
                }
            }
        }

        return index;
    }

    private void StripExistingStructure(PdfDocument pdf)
    {
        var catalog = pdf.GetCatalog().GetPdfObject();
        var treeRoot = catalog.GetAsDictionary(PdfName.StructTreeRoot);
        if (treeRoot != null)
        {
            treeRoot.Remove(PdfName.K);
            treeRoot.Remove(new PdfName("ParentTree"));
            treeRoot.Remove(new PdfName("ParentTreeNextKey"));
        }
    }

    private void CreateNode(PdfDocument pdf, PdfStructElem parent, StructureNode node, Dictionary<string, PdfStructElem> index)
    {
        var elem = new PdfStructElem(pdf, new PdfName(node.Role));
        parent.AddKid(elem);

        // Set language
        if (!string.IsNullOrEmpty(node.Language))
        {
            elem.GetPdfObject().Put(PdfName.Lang, new PdfString(node.Language));
        }

        // Set alt text
        if (!string.IsNullOrEmpty(node.AltText))
        {
            elem.GetPdfObject().Put(PdfName.Alt, new PdfString(node.AltText));
        }

        // Set actual text
        if (!string.IsNullOrEmpty(node.ActualText))
        {
            elem.GetPdfObject().Put(new PdfName("ActualText"), new PdfString(node.ActualText));
        }

        // Set element ID
        if (!string.IsNullOrEmpty(node.ElementId))
        {
            elem.GetPdfObject().Put(new PdfName("ID"), new PdfString(node.ElementId));
        }

        // Build attributes
        var attributeDicts = new List<PdfDictionary>();

        // BBox attribute (Layout owner)
        if (node.Bbox != null)
        {
            var layoutAttr = new PdfDictionary();
            layoutAttr.Put(PdfName.O, new PdfName("Layout"));
            layoutAttr.Put(new PdfName("BBox"), new PdfArray(new float[]
            {
                node.Bbox.X,
                node.Bbox.Y,
                node.Bbox.X + node.Bbox.Width,
                node.Bbox.Y + node.Bbox.Height
            }));
            attributeDicts.Add(layoutAttr);
        }

        // Table attributes (Scope, ColSpan, RowSpan)
        if (!string.IsNullOrEmpty(node.Scope) || node.ColSpan.HasValue || node.RowSpan.HasValue)
        {
            var tableAttr = new PdfDictionary();
            tableAttr.Put(PdfName.O, new PdfName("Table"));

            if (!string.IsNullOrEmpty(node.Scope))
            {
                tableAttr.Put(new PdfName("Scope"), new PdfName(node.Scope));
            }

            if (node.ColSpan.HasValue)
            {
                tableAttr.Put(new PdfName("ColSpan"), new PdfNumber(node.ColSpan.Value));
            }

            if (node.RowSpan.HasValue)
            {
                tableAttr.Put(new PdfName("RowSpan"), new PdfNumber(node.RowSpan.Value));
            }

            attributeDicts.Add(tableAttr);
        }

        // Generic attributes - group by Owner
        if (node.Attributes != null && node.Attributes.Count > 0)
        {
            var byOwner = new Dictionary<string, PdfDictionary>();

            foreach (var attr in node.Attributes)
            {
                if (!byOwner.TryGetValue(attr.Owner, out var ownerDict))
                {
                    ownerDict = new PdfDictionary();
                    ownerDict.Put(PdfName.O, new PdfName(attr.Owner));
                    byOwner[attr.Owner] = ownerDict;
                }

                ownerDict.Put(new PdfName(attr.Key), new PdfString(attr.Value));
            }

            // Merge with existing attribute dicts by owner, or add new ones
            foreach (var kvp in byOwner)
            {
                var existing = attributeDicts.Find(d =>
                {
                    var o = d.GetAsName(PdfName.O);
                    return o != null && o.GetValue() == kvp.Key;
                });

                if (existing != null)
                {
                    // Merge keys from kvp.Value into existing
                    foreach (var key in kvp.Value.KeySet())
                    {
                        if (!key.Equals(PdfName.O))
                        {
                            existing.Put(key, kvp.Value.Get(key));
                        }
                    }
                }
                else
                {
                    attributeDicts.Add(kvp.Value);
                }
            }
        }

        // Set /A on the element
        if (attributeDicts.Count == 1)
        {
            elem.GetPdfObject().Put(PdfName.A, attributeDicts[0]);
        }
        else if (attributeDicts.Count > 1)
        {
            var arr = new PdfArray();
            foreach (var d in attributeDicts)
            {
                arr.Add(d);
            }
            elem.GetPdfObject().Put(PdfName.A, arr);
        }

        // Add to index if id is provided
        if (!string.IsNullOrEmpty(node.Id))
        {
            index[node.Id] = elem;
        }

        // Recursively create children
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                CreateNode(pdf, elem, child, index);
            }
        }
    }
}
