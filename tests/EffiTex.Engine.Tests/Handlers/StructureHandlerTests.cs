using EffiTex.Core.Models;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class StructureHandlerTests
{
    [Fact]
    public void Apply_SimpleTree_CreatesDocumentWithH1AndP()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new() { Id = "h1", Role = "H1" },
                        new() { Id = "p1", Role = "P" }
                    }
                };
                var index = handler.Apply(pdf, instruction);

                index.Should().ContainKey("h1");
                index.Should().ContainKey("p1");
            }

            // Re-open and verify persisted structure
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                root.Should().NotBeNull();

                var kids = root.GetKids();
                kids.Should().NotBeNull();
                kids.Should().HaveCountGreaterThanOrEqualTo(1);

                // The first kid should be the Document root element
                var docElem = kids[0] as PdfStructElem;
                docElem.Should().NotBeNull();
                docElem.GetRole().GetValue().Should().Be("Document");

                // Document should have 2 children: H1 and P
                var docKids = docElem.GetKids();
                docKids.Should().HaveCount(2);

                var h1Elem = docKids[0] as PdfStructElem;
                h1Elem.Should().NotBeNull();
                h1Elem.GetRole().GetValue().Should().Be("H1");

                var pElem = docKids[1] as PdfStructElem;
                pElem.Should().NotBeNull();
                pElem.GetRole().GetValue().Should().Be("P");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_TableTree_CreatesScopeColSpanRowSpan()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new()
                        {
                            Id = "table1",
                            Role = "Table",
                            Children = new List<StructureNode>
                            {
                                new()
                                {
                                    Id = "tr1",
                                    Role = "TR",
                                    Children = new List<StructureNode>
                                    {
                                        new()
                                        {
                                            Id = "th1",
                                            Role = "TH",
                                            Scope = "Column",
                                            ColSpan = 2,
                                            RowSpan = 1
                                        },
                                        new()
                                        {
                                            Id = "td1",
                                            Role = "TD",
                                            ColSpan = 1,
                                            RowSpan = 3
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var index = handler.Apply(pdf, instruction);

                index.Should().ContainKey("table1");
                index.Should().ContainKey("tr1");
                index.Should().ContainKey("th1");
                index.Should().ContainKey("td1");
            }

            // Re-open and verify table attributes
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var tableElem = docElem.GetKids()[0] as PdfStructElem;
                tableElem.GetRole().GetValue().Should().Be("Table");

                var trElem = tableElem.GetKids()[0] as PdfStructElem;
                trElem.GetRole().GetValue().Should().Be("TR");

                var thElem = trElem.GetKids()[0] as PdfStructElem;
                thElem.GetRole().GetValue().Should().Be("TH");

                // Check TH attributes: scope, colspan, rowspan
                var thA = thElem.GetPdfObject().Get(PdfName.A);
                thA.Should().NotBeNull();

                // TH should have Table owner attributes
                var thAttrDict = FindAttributeByOwner(thA, "Table");
                thAttrDict.Should().NotBeNull();
                thAttrDict.GetAsName(new PdfName("Scope")).GetValue().Should().Be("Column");
                thAttrDict.GetAsNumber(new PdfName("ColSpan")).IntValue().Should().Be(2);
                thAttrDict.GetAsNumber(new PdfName("RowSpan")).IntValue().Should().Be(1);

                // Check TD attributes: colspan, rowspan (no scope)
                var tdElem = trElem.GetKids()[1] as PdfStructElem;
                tdElem.GetRole().GetValue().Should().Be("TD");

                var tdA = tdElem.GetPdfObject().Get(PdfName.A);
                tdA.Should().NotBeNull();

                var tdAttrDict = FindAttributeByOwner(tdA, "Table");
                tdAttrDict.Should().NotBeNull();
                tdAttrDict.GetAsNumber(new PdfName("ColSpan")).IntValue().Should().Be(1);
                tdAttrDict.GetAsNumber(new PdfName("RowSpan")).IntValue().Should().Be(3);
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_StripExistingTrue_RemovesExistingTagTree()
    {
        // Create a tagged PDF first
        var taggedTemp = Path.GetTempFileName() + ".pdf";
        var finalTemp = Path.GetTempFileName() + ".pdf";
        try
        {
            // First create a tagged doc
            using (var writer = new PdfWriter(taggedTemp))
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var oldElem = new PdfStructElem(pdf, new PdfName("OldRoot"));
                structRoot.AddKid(oldElem);
                var page = pdf.AddNewPage();
                pdf.Close();
            }

            // Now apply with StripExisting = true
            using (var reader = new PdfReader(taggedTemp))
            using (var writer = new PdfWriter(finalTemp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = true,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new() { Id = "new1", Role = "P" }
                    }
                };
                handler.Apply(pdf, instruction);
            }

            // Verify old structure is gone, new structure exists
            using (var reader = new PdfReader(finalTemp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                root.Should().NotBeNull();

                var kids = root.GetKids();
                // Should only have the new Document element, not OldRoot
                kids.Should().HaveCount(1);
                var docElem = kids[0] as PdfStructElem;
                docElem.Should().NotBeNull();
                docElem.GetRole().GetValue().Should().Be("Document");
            }
        }
        finally
        {
            File.Delete(taggedTemp);
            File.Delete(finalTemp);
        }
    }

    [Fact]
    public void Apply_StripExistingFalse_LeavesExistingTreeIntact()
    {
        // Create a tagged PDF first
        var taggedTemp = Path.GetTempFileName() + ".pdf";
        var finalTemp = Path.GetTempFileName() + ".pdf";
        try
        {
            // First create a tagged doc with existing structure
            using (var writer = new PdfWriter(taggedTemp))
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetTagged();
                var structRoot = pdf.GetStructTreeRoot();
                var oldElem = new PdfStructElem(pdf, new PdfName("OldRoot"));
                structRoot.AddKid(oldElem);
                var page = pdf.AddNewPage();
                pdf.Close();
            }

            // Now apply with StripExisting = false
            using (var reader = new PdfReader(taggedTemp))
            using (var writer = new PdfWriter(finalTemp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new() { Id = "new1", Role = "P" }
                    }
                };
                handler.Apply(pdf, instruction);
            }

            // Verify both old and new structures exist
            using (var reader = new PdfReader(finalTemp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                root.Should().NotBeNull();

                var kids = root.GetKids();
                // Should have OldRoot AND new Document element
                kids.Should().HaveCount(2);

                var oldElem = kids[0] as PdfStructElem;
                oldElem.Should().NotBeNull();
                oldElem.GetRole().GetValue().Should().Be("OldRoot");

                var docElem = kids[1] as PdfStructElem;
                docElem.Should().NotBeNull();
                docElem.GetRole().GetValue().Should().Be("Document");
            }
        }
        finally
        {
            File.Delete(taggedTemp);
            File.Delete(finalTemp);
        }
    }

    [Fact]
    public void Apply_FigureNode_HasCorrectAltText()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new()
                        {
                            Id = "fig1",
                            Role = "Figure",
                            AltText = "A beautiful sunset over the ocean"
                        }
                    }
                };
                handler.Apply(pdf, instruction);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var figElem = docElem.GetKids()[0] as PdfStructElem;
                figElem.GetRole().GetValue().Should().Be("Figure");

                var altText = figElem.GetPdfObject().GetAsString(PdfName.Alt);
                altText.Should().NotBeNull();
                altText.GetValue().Should().Be("A beautiful sunset over the ocean");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_ElementId_SetsIdAttribute()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new()
                        {
                            Id = "node1",
                            Role = "P",
                            ElementId = "para-001"
                        }
                    }
                };
                handler.Apply(pdf, instruction);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var pElem = docElem.GetKids()[0] as PdfStructElem;

                var idVal = pElem.GetPdfObject().GetAsString(new PdfName("ID"));
                idVal.Should().NotBeNull();
                idVal.GetValue().Should().Be("para-001");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_LanguageAtNodeLevel_SetsLangProperty()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new()
                        {
                            Id = "span_fr",
                            Role = "Span",
                            Language = "fr-FR"
                        }
                    }
                };
                handler.Apply(pdf, instruction);
            }

            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var spanElem = docElem.GetKids()[0] as PdfStructElem;

                var lang = spanElem.GetPdfObject().GetAsString(PdfName.Lang);
                lang.Should().NotBeNull();
                lang.GetValue().Should().Be("fr-FR");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_ReturnsDictionaryMappingAllNodeIds()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new()
                        {
                            Id = "a",
                            Role = "H1",
                            Children = new List<StructureNode>
                            {
                                new() { Id = "b", Role = "Span" }
                            }
                        },
                        new() { Id = "c", Role = "P" },
                        new()
                        {
                            Id = "d",
                            Role = "Table",
                            Children = new List<StructureNode>
                            {
                                new()
                                {
                                    Id = "e",
                                    Role = "TR",
                                    Children = new List<StructureNode>
                                    {
                                        new() { Id = "f", Role = "TD" }
                                    }
                                }
                            }
                        }
                    }
                };
                var index = handler.Apply(pdf, instruction);

                index.Should().HaveCount(6);
                index.Should().ContainKey("a");
                index.Should().ContainKey("b");
                index.Should().ContainKey("c");
                index.Should().ContainKey("d");
                index.Should().ContainKey("e");
                index.Should().ContainKey("f");

                // Verify each maps to a valid PdfStructElem
                foreach (var kvp in index)
                {
                    kvp.Value.Should().NotBeNull();
                    kvp.Value.Should().BeOfType<PdfStructElem>();
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Apply_NodeWithNoId_NotInDictionaryButStillCreated()
    {
        var source = FixtureGenerator.EnsureUntaggedSimple();
        var temp = Path.GetTempFileName() + ".pdf";
        try
        {
            using (var reader = new PdfReader(source))
            using (var writer = new PdfWriter(temp))
            using (var pdf = new PdfDocument(reader, writer))
            {
                var handler = new StructureHandler();
                var instruction = new StructureInstruction
                {
                    StripExisting = false,
                    Root = "Document",
                    Children = new List<StructureNode>
                    {
                        new() { Id = "h1", Role = "H1" },
                        new() { Role = "P" },  // No Id
                        new() { Id = "h2", Role = "H2" }
                    }
                };
                var index = handler.Apply(pdf, instruction);

                // Only nodes with ids should be in the dictionary
                index.Should().HaveCount(2);
                index.Should().ContainKey("h1");
                index.Should().ContainKey("h2");
                // Verify no null keys exist (null Id nodes are excluded)
                index.Keys.Should().NotContainNulls();
            }

            // But the P element should still exist in the tree
            using (var reader = new PdfReader(temp))
            using (var pdf = new PdfDocument(reader))
            {
                var root = pdf.GetStructTreeRoot();
                var docElem = root.GetKids()[0] as PdfStructElem;
                var kids = docElem.GetKids();
                kids.Should().HaveCount(3);

                (kids[0] as PdfStructElem).GetRole().GetValue().Should().Be("H1");
                (kids[1] as PdfStructElem).GetRole().GetValue().Should().Be("P");
                (kids[2] as PdfStructElem).GetRole().GetValue().Should().Be("H2");
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// Helper: finds an attribute dictionary with a given Owner name from the /A entry.
    /// /A can be a single PdfDictionary or a PdfArray of PdfDictionary.
    /// </summary>
    private static PdfDictionary FindAttributeByOwner(PdfObject aEntry, string owner)
    {
        if (aEntry is PdfDictionary dict)
        {
            var o = dict.GetAsName(PdfName.O);
            if (o != null && o.GetValue() == owner)
                return dict;
            return null;
        }

        if (aEntry is PdfArray arr)
        {
            for (int i = 0; i < arr.Size(); i++)
            {
                var item = arr.GetAsDictionary(i);
                if (item != null)
                {
                    var o = item.GetAsName(PdfName.O);
                    if (o != null && o.GetValue() == owner)
                        return item;
                }
            }
        }

        return null;
    }
}
