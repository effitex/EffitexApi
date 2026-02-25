using EffiTex.Core.Models;
using FluentAssertions;
using Xunit;

namespace EffiTex.Core.Tests.Models;

public class BoundingBoxTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var bbox = new BoundingBox { X = 72f, Y = 680f, Width = 468f, Height = 24f };

        bbox.X.Should().Be(72f);
        bbox.Y.Should().Be(680f);
        bbox.Width.Should().Be(468f);
        bbox.Height.Should().Be(24f);
    }

    [Fact]
    public void Properties_ZeroValues_AreValid()
    {
        var bbox = new BoundingBox { X = 0f, Y = 0f, Width = 0f, Height = 0f };

        bbox.X.Should().Be(0f);
        bbox.Y.Should().Be(0f);
        bbox.Width.Should().Be(0f);
        bbox.Height.Should().Be(0f);
    }
}

public class InstructionSetTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction { Language = "en-US" },
            Structure = new StructureInstruction { Root = "Document" },
            ContentTagging = new List<ContentTaggingEntry> { new() { Node = "h1", Page = 1 } },
            Artifacts = new List<ArtifactEntry> { new() { Page = 1, Type = "layout" } },
            Annotations = new List<AnnotationOperation> { new() { Op = "set_contents" } },
            Bookmarks = new BookmarksInstruction { GenerateFromHeadings = true },
            Fonts = new List<FontOperation> { new() { Op = "write_cidset" } },
            Ocr = new List<OcrPage> { new() { Page = 1 } }
        };

        set.Version.Should().Be("1.0");
        set.Metadata.Should().NotBeNull();
        set.Structure.Should().NotBeNull();
        set.ContentTagging.Should().HaveCount(1);
        set.Artifacts.Should().HaveCount(1);
        set.Annotations.Should().HaveCount(1);
        set.Bookmarks.Should().NotBeNull();
        set.Fonts.Should().HaveCount(1);
        set.Ocr.Should().HaveCount(1);
    }

    [Fact]
    public void Properties_NoSectionsPopulated_SectionsAreNull()
    {
        var set = new InstructionSet();

        set.Metadata.Should().BeNull();
        set.Structure.Should().BeNull();
        set.ContentTagging.Should().BeNull();
        set.Artifacts.Should().BeNull();
        set.Annotations.Should().BeNull();
        set.Bookmarks.Should().BeNull();
        set.Fonts.Should().BeNull();
        set.Ocr.Should().BeNull();
    }
}

public class MetadataInstructionTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var metadata = new MetadataInstruction
        {
            Language = "en-US",
            Title = "Test",
            DisplayDocTitle = true,
            MarkInfo = true,
            PdfUaIdentifier = 1,
            TabOrder = "structure"
        };

        metadata.Language.Should().Be("en-US");
        metadata.Title.Should().Be("Test");
        metadata.DisplayDocTitle.Should().BeTrue();
        metadata.MarkInfo.Should().BeTrue();
        metadata.PdfUaIdentifier.Should().Be(1);
        metadata.TabOrder.Should().Be("structure");
    }
}

public class StructureInstructionTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var structure = new StructureInstruction
        {
            StripExisting = true,
            Root = "Document",
            Children = new List<StructureNode> { new() { Role = "P" } }
        };

        structure.StripExisting.Should().BeTrue();
        structure.Root.Should().Be("Document");
        structure.Children.Should().HaveCount(1);
    }
}

public class StructureNodeTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var node = new StructureNode
        {
            Id = "h1-1",
            Role = "H1",
            Bbox = new BoundingBox { X = 72, Y = 680, Width = 468, Height = 24 },
            Language = "en-US",
            AltText = "Heading",
            ActualText = "Heading Text",
            ElementId = "elem-1",
            Scope = "Column",
            ColSpan = 2,
            RowSpan = 1,
            Attributes = new List<StructureAttribute>
            {
                new() { Owner = "Table", Key = "Headers", Value = "th-0" }
            },
            Children = new List<StructureNode> { new() { Role = "P" } }
        };

        node.Id.Should().Be("h1-1");
        node.Role.Should().Be("H1");
        node.Bbox.Should().NotBeNull();
        node.Language.Should().Be("en-US");
        node.AltText.Should().Be("Heading");
        node.ActualText.Should().Be("Heading Text");
        node.ElementId.Should().Be("elem-1");
        node.Scope.Should().Be("Column");
        node.ColSpan.Should().Be(2);
        node.RowSpan.Should().Be(1);
        node.Attributes.Should().HaveCount(1);
        node.Children.Should().HaveCount(1);
    }

    [Fact]
    public void Children_NoChildrenSet_IsEmptyList()
    {
        var node = new StructureNode { Role = "P" };

        node.Children.Should().NotBeNull();
        node.Children.Should().BeEmpty();
    }
}

public class StructureAttributeTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var attr = new StructureAttribute
        {
            Owner = "Table",
            Key = "Headers",
            Value = "th-0"
        };

        attr.Owner.Should().Be("Table");
        attr.Key.Should().Be("Headers");
        attr.Value.Should().Be("th-0");
    }
}

public class ContentTaggingEntryTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var entry = new ContentTaggingEntry
        {
            Node = "h1-1",
            Page = 1,
            Bbox = new BoundingBox { X = 72, Y = 680, Width = 468, Height = 24 }
        };

        entry.Node.Should().Be("h1-1");
        entry.Page.Should().Be(1);
        entry.Bbox.Should().NotBeNull();
    }
}

public class ArtifactEntryTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var entry = new ArtifactEntry
        {
            Page = 1,
            Bbox = new BoundingBox { X = 0, Y = 750, Width = 612, Height = 42 },
            Type = "layout"
        };

        entry.Page.Should().Be(1);
        entry.Bbox.Should().NotBeNull();
        entry.Type.Should().Be("layout");
    }
}

public class AnnotationOperationTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var op = new AnnotationOperation
        {
            Op = "create_widget",
            Page = 1,
            Index = 0,
            Value = "test",
            Node = "form-1",
            Rect = new BoundingBox { X = 100, Y = 300, Width = 200, Height = 20 },
            FieldName = "last_name",
            FieldType = "Tx",
            Tu = "Last Name"
        };

        op.Op.Should().Be("create_widget");
        op.Page.Should().Be(1);
        op.Index.Should().Be(0);
        op.Value.Should().Be("test");
        op.Node.Should().Be("form-1");
        op.Rect.Should().NotBeNull();
        op.FieldName.Should().Be("last_name");
        op.FieldType.Should().Be("Tx");
        op.Tu.Should().Be("Last Name");
    }
}

public class BookmarksInstructionTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var bookmarks = new BookmarksInstruction { GenerateFromHeadings = true };

        bookmarks.GenerateFromHeadings.Should().BeTrue();
    }
}

public class FontOperationTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var op = new FontOperation
        {
            Op = "write_cidset",
            Font = "CIDFont+F1",
            Page = 1,
            Cids = new List<int> { 0, 1, 2 },
            GlyphNames = new List<string> { "A", "B" },
            Encoding = "WinAnsiEncoding",
            Differences = new Dictionary<int, string> { { 128, "Euro" } },
            Mappings = new Dictionary<int, string> { { 65, "A" } },
            Widths = new Dictionary<int, float> { { 42, 600f } }
        };

        op.Op.Should().Be("write_cidset");
        op.Font.Should().Be("CIDFont+F1");
        op.Page.Should().Be(1);
        op.Cids.Should().HaveCount(3);
        op.GlyphNames.Should().HaveCount(2);
        op.Encoding.Should().Be("WinAnsiEncoding");
        op.Differences.Should().HaveCount(1);
        op.Mappings.Should().HaveCount(1);
        op.Widths.Should().HaveCount(1);
    }
}

public class OcrPageTests
{
    [Fact]
    public void Properties_AllPopulated_ReturnAssignedValues()
    {
        var page = new OcrPage
        {
            Page = 1,
            Words = new List<OcrWord>
            {
                new()
                {
                    Text = "MEETING",
                    Bbox = new BoundingBox { X = 72, Y = 720, Width = 80, Height = 14 },
                    Confidence = 0.98f
                }
            }
        };

        page.Page.Should().Be(1);
        page.Words.Should().HaveCount(1);
        page.Words[0].Text.Should().Be("MEETING");
        page.Words[0].Confidence.Should().Be(0.98f);
    }
}
