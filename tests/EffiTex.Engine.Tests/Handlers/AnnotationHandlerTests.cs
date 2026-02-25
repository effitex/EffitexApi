using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class AnnotationHandlerTests
{
    [Fact]
    public void Apply_SetContents_SetsAnnotationContents()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new AnnotationHandler();
                handler.Apply(pdf, new List<AnnotationOperation>
                {
                    new() { Op = "set_contents", Page = 1, Index = 0, Value = "Updated link text" }
                }, new Dictionary<string, PdfStructElem>());
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var annots = page.GetAnnotations();
                annots[0].GetPdfObject().GetAsString(PdfName.Contents)
                    .GetValue().Should().Be("Updated link text");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_SetTu_SetsWidgetTU()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new AnnotationHandler();
                handler.Apply(pdf, new List<AnnotationOperation>
                {
                    new() { Op = "set_tu", Page = 1, Index = 1, Value = "Updated Tooltip" }
                }, new Dictionary<string, PdfStructElem>());
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var annots = page.GetAnnotations();
                annots[1].GetPdfObject().GetAsString(new PdfName("TU"))
                    .GetValue().Should().Be("Updated Tooltip");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_Associate_CreatesObjrAndSetsStructParent()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var docElem = new PdfStructElem(pdf, new PdfName("Document"));
                structRoot.AddKid(docElem);
                var linkElem = new PdfStructElem(pdf, new PdfName("Link"));
                docElem.AddKid(linkElem);

                var nodeIndex = new Dictionary<string, PdfStructElem>
                {
                    ["link-1"] = linkElem
                };

                var handler = new AnnotationHandler();
                handler.Apply(pdf, new List<AnnotationOperation>
                {
                    new() { Op = "associate", Page = 1, Index = 0, Node = "link-1" }
                }, nodeIndex);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var annots = page.GetAnnotations();
                annots[0].GetPdfObject().Get(new PdfName("StructParent"))
                    .Should().NotBeNull();
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_CreateWidget_CreatesAnnotationWithCorrectProperties()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            int initialAnnotCount;
            using (var reader = new PdfReader(source))
            using (var pdf = new PdfDocument(reader))
            {
                initialAnnotCount = pdf.GetPage(1).GetAnnotations().Count;
            }

            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new AnnotationHandler();
                handler.Apply(pdf, new List<AnnotationOperation>
                {
                    new()
                    {
                        Op = "create_widget", Page = 1,
                        Rect = new BoundingBox { X = 200, Y = 400, Width = 150, Height = 25 },
                        FieldName = "last_name", FieldType = "Tx", Tu = "Last Name"
                    }
                }, new Dictionary<string, PdfStructElem>());
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var page = pdf.GetPage(1);
                var annots = page.GetAnnotations();
                annots.Count.Should().Be(initialAnnotCount + 1);

                var lastAnnot = annots[annots.Count - 1].GetPdfObject();
                lastAnnot.GetAsName(PdfName.Subtype).GetValue().Should().Be("Widget");
                lastAnnot.GetAsString(new PdfName("T")).GetValue().Should().Be("last_name");
                lastAnnot.GetAsName(new PdfName("FT")).GetValue().Should().Be("Tx");
                lastAnnot.GetAsString(new PdfName("TU")).GetValue().Should().Be("Last Name");
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_CreateWidget_AddsToAcroForm()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new AnnotationHandler();
                handler.Apply(pdf, new List<AnnotationOperation>
                {
                    new()
                    {
                        Op = "create_widget", Page = 1,
                        Rect = new BoundingBox { X = 100, Y = 300, Width = 200, Height = 20 },
                        FieldName = "email", FieldType = "Tx"
                    }
                }, new Dictionary<string, PdfStructElem>());
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var acroForm = pdf.GetCatalog().GetPdfObject()
                    .GetAsDictionary(new PdfName("AcroForm"));
                acroForm.Should().NotBeNull();
                var fields = acroForm.GetAsArray(new PdfName("Fields"));
                fields.Should().NotBeNull();
                fields.Size().Should().BeGreaterThan(0);
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_InvalidPageOrIndex_ThrowsDescriptiveException()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using var reader = new PdfReader(source);
            using var writer = new PdfWriter(temp);
            using var pdf = new PdfDocument(reader, writer);

            var handler = new AnnotationHandler();
            var act = () => handler.Apply(pdf, new List<AnnotationOperation>
            {
                new() { Op = "set_contents", Page = 1, Index = 999, Value = "test" }
            }, new Dictionary<string, PdfStructElem>());

            act.Should().Throw<InvalidOperationException>();
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Apply_NullOperations_IsNoOp()
    {
        var source = FixtureGenerator.EnsureAnnotated();
        var temp = System.IO.Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new AnnotationHandler();
                handler.Apply(pdf, null, new Dictionary<string, PdfStructElem>());
            }

            File.Exists(temp).Should().BeTrue();
        }
        finally { File.Delete(temp); }
    }
}
