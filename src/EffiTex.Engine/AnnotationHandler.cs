using System.Text;
using EffiTex.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;

namespace EffiTex.Engine;

public class AnnotationHandler
{
    public void Apply(PdfDocument pdf, List<AnnotationOperation> operations, Dictionary<string, PdfStructElem> nodeIndex)
    {
        if (operations == null) return;

        foreach (var op in operations)
        {
            switch (op.Op)
            {
                case "set_contents":
                    setContents(pdf, op);
                    break;
                case "set_tu":
                    setTu(pdf, op);
                    break;
                case "associate":
                    associate(pdf, op, nodeIndex);
                    break;
                case "create_widget":
                    createWidget(pdf, op);
                    break;
            }
        }
    }

    private static void setContents(PdfDocument pdf, AnnotationOperation op)
    {
        var annot = getAnnotation(pdf, op.Page, op.Index.Value);
        annot.GetPdfObject().Put(PdfName.Contents, new PdfString(op.Value));
    }

    private static void setTu(PdfDocument pdf, AnnotationOperation op)
    {
        var annot = getAnnotation(pdf, op.Page, op.Index.Value);
        annot.GetPdfObject().Put(new PdfName("TU"), new PdfString(op.Value));
    }

    private static void associate(PdfDocument pdf, AnnotationOperation op, Dictionary<string, PdfStructElem> nodeIndex)
    {
        var annot = getAnnotation(pdf, op.Page, op.Index.Value);
        var page = pdf.GetPage(op.Page);

        if (!nodeIndex.TryGetValue(op.Node, out var structElem))
        {
            throw new InvalidOperationException($"Structure node \"{op.Node}\" not found in node index.");
        }

        var annotObj = annot.GetPdfObject();
        var objr = new PdfDictionary();
        objr.Put(PdfName.Type, new PdfName("OBJR"));
        objr.Put(new PdfName("Obj"), annotObj);
        objr.Put(PdfName.Pg, page.GetPdfObject());
        objr.MakeIndirect(pdf);

        var elemDict = structElem.GetPdfObject();
        var existingK = elemDict.Get(PdfName.K);
        if (existingK == null)
        {
            elemDict.Put(PdfName.K, objr);
        }
        else if (existingK is PdfArray kArray)
        {
            kArray.Add(objr);
        }
        else
        {
            var newArray = new PdfArray();
            newArray.Add(existingK);
            newArray.Add(objr);
            elemDict.Put(PdfName.K, newArray);
        }

        var structTreeRoot = pdf.GetStructTreeRoot();
        var parentTreeNextKey = structTreeRoot.GetPdfObject()
            .GetAsNumber(new PdfName("ParentTreeNextKey"));
        int nextKey = parentTreeNextKey?.IntValue() ?? 0;

        annotObj.Put(new PdfName("StructParent"), new PdfNumber(nextKey));

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
        numsArray.Add(new PdfNumber(nextKey));
        numsArray.Add(structElem.GetPdfObject());

        structTreeRoot.GetPdfObject()
            .Put(new PdfName("ParentTreeNextKey"), new PdfNumber(nextKey + 1));
    }

    private static void createWidget(PdfDocument pdf, AnnotationOperation op)
    {
        var page = pdf.GetPage(op.Page);

        var widget = new PdfDictionary();
        widget.Put(PdfName.Type, PdfName.Annot);
        widget.Put(PdfName.Subtype, new PdfName("Widget"));
        widget.Put(new PdfName("T"), new PdfString(op.FieldName));
        widget.Put(new PdfName("FT"), new PdfName(op.FieldType));
        widget.Put(new PdfName("F"), new PdfNumber(4));

        if (op.Tu != null)
        {
            widget.Put(new PdfName("TU"), new PdfString(op.Tu));
        }

        var rect = op.Rect;
        var rectArray = new PdfArray(new float[]
        {
            rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height
        });
        widget.Put(PdfName.Rect, rectArray);

        createMinimalAppearance(widget, rect.Width, rect.Height, op.FieldType);
        widget.MakeIndirect(pdf);

        var annots = page.GetPdfObject().GetAsArray(PdfName.Annots);
        if (annots == null)
        {
            annots = new PdfArray();
            page.GetPdfObject().Put(PdfName.Annots, annots);
        }
        annots.Add(widget);

        var catalog = pdf.GetCatalog().GetPdfObject();
        var acroForm = catalog.GetAsDictionary(new PdfName("AcroForm"));
        if (acroForm == null)
        {
            acroForm = new PdfDictionary();
            acroForm.MakeIndirect(pdf);
            catalog.Put(new PdfName("AcroForm"), acroForm);
        }

        var fields = acroForm.GetAsArray(new PdfName("Fields"));
        if (fields == null)
        {
            fields = new PdfArray();
            acroForm.Put(new PdfName("Fields"), fields);
        }
        fields.Add(widget);
    }

    private static void createMinimalAppearance(PdfDictionary widget, float width, float height, string fieldType)
    {
        var stream = new PdfStream();
        var bbox = new PdfArray(new float[] { 0, 0, width, height });
        stream.Put(PdfName.BBox, bbox);
        stream.Put(PdfName.Type, PdfName.XObject);
        stream.Put(PdfName.Subtype, PdfName.Form);

        string drawCommands = fieldType == "Btn"
            ? $"0 0 {width} {height} re S"
            : $"0 0 m {width} 0 l S";

        stream.SetData(Encoding.ASCII.GetBytes(drawCommands));

        var ap = new PdfDictionary();
        ap.Put(new PdfName("N"), stream);
        widget.Put(new PdfName("AP"), ap);
    }

    private static iText.Kernel.Pdf.Annot.PdfAnnotation getAnnotation(PdfDocument pdf, int pageNumber, int index)
    {
        if (pageNumber < 1 || pageNumber > pdf.GetNumberOfPages())
        {
            throw new InvalidOperationException($"Page {pageNumber} does not exist in document with {pdf.GetNumberOfPages()} pages.");
        }

        var page = pdf.GetPage(pageNumber);
        var annotations = page.GetAnnotations();

        if (index < 0 || index >= annotations.Count)
        {
            throw new InvalidOperationException($"Annotation index {index} is out of range. Page {pageNumber} has {annotations.Count} annotations.");
        }

        return annotations[index];
    }
}
