using EffiTex.Core.Deserialization;
using EffiTex.Core.Models;
using FluentAssertions;
using Xunit;

namespace EffiTex.Core.Tests.Deserialization;

public class DeserializationTests
{
    private static readonly string _fixturesPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    private string readFixture(string filename)
    {
        return File.ReadAllText(Path.Combine(_fixturesPath, filename));
    }

    [Fact]
    public void FullInstruction_Yaml_AllSectionsPopulated()
    {
        var yaml = readFixture("full_instruction.yaml");
        var result = new YamlDeserializer().Deserialize(yaml);

        assertFullInstruction(result);
    }

    [Fact]
    public void FullInstruction_Json_AllSectionsPopulated()
    {
        var json = readFixture("full_instruction.json");
        var result = new JsonDeserializer().Deserialize(json);

        assertFullInstruction(result);
    }

    [Fact]
    public void FullInstruction_YamlAndJson_ProduceIdenticalResults()
    {
        var yaml = readFixture("full_instruction.yaml");
        var json = readFixture("full_instruction.json");

        var yamlResult = new YamlDeserializer().Deserialize(yaml);
        var jsonResult = new JsonDeserializer().Deserialize(json);

        yamlResult.Version.Should().Be(jsonResult.Version);
        yamlResult.Metadata.Language.Should().Be(jsonResult.Metadata.Language);
        yamlResult.Metadata.Title.Should().Be(jsonResult.Metadata.Title);
        yamlResult.Structure.Root.Should().Be(jsonResult.Structure.Root);
        yamlResult.Structure.StripExisting.Should().Be(jsonResult.Structure.StripExisting);
        yamlResult.Structure.Children.Should().HaveCount(jsonResult.Structure.Children.Count);
        yamlResult.ContentTagging.Should().HaveCount(jsonResult.ContentTagging.Count);
        yamlResult.Artifacts.Should().HaveCount(jsonResult.Artifacts.Count);
        yamlResult.Annotations.Should().HaveCount(jsonResult.Annotations.Count);
        yamlResult.Bookmarks.GenerateFromHeadings.Should().Be(jsonResult.Bookmarks.GenerateFromHeadings);
        yamlResult.Fonts.Should().HaveCount(jsonResult.Fonts.Count);
        yamlResult.Ocr.Should().HaveCount(jsonResult.Ocr.Count);
    }

    [Fact]
    public void MetadataOnly_Yaml_NonMetadataSectionsAreNull()
    {
        var yaml = readFixture("metadata_only.yaml");
        var result = new YamlDeserializer().Deserialize(yaml);

        result.Version.Should().Be("1.0");
        result.Metadata.Should().NotBeNull();
        result.Metadata.Language.Should().Be("en-US");
        result.Structure.Should().BeNull();
        result.ContentTagging.Should().BeNull();
        result.Artifacts.Should().BeNull();
        result.Annotations.Should().BeNull();
        result.Bookmarks.Should().BeNull();
        result.Fonts.Should().BeNull();
        result.Ocr.Should().BeNull();
    }

    [Fact]
    public void StructureNested_Yaml_TreeDepthPreserved()
    {
        var yaml = readFixture("structure_nested.yaml");
        var result = new YamlDeserializer().Deserialize(yaml);

        result.Structure.Should().NotBeNull();
        result.Structure.Root.Should().Be("Document");
        result.Structure.StripExisting.Should().BeFalse();

        var sect = result.Structure.Children[0];
        sect.Id.Should().Be("sect-1");
        sect.Role.Should().Be("Sect");
        sect.Language.Should().Be("en-US");

        var table = sect.Children[0];
        table.Id.Should().Be("table-1");
        table.Role.Should().Be("Table");

        var thead = table.Children[0];
        thead.Role.Should().Be("THead");

        var headerRow = thead.Children[0];
        headerRow.Role.Should().Be("TR");

        var th0 = headerRow.Children[0];
        th0.Id.Should().Be("th-0");
        th0.Role.Should().Be("TH");
        th0.ElementId.Should().Be("th-hdr-0");
        th0.Scope.Should().Be("Column");

        var tbody = table.Children[1];
        tbody.Role.Should().Be("TBody");

        var dataRow = tbody.Children[0];
        var td0 = dataRow.Children[0];
        td0.Id.Should().Be("td-0");
        td0.ColSpan.Should().Be(2);
        td0.RowSpan.Should().Be(1);
        td0.Attributes.Should().HaveCount(1);
        td0.Attributes[0].Owner.Should().Be("Table");
        td0.Attributes[0].Key.Should().Be("Headers");
    }

    [Fact]
    public void FontsAllOps_Yaml_EachOperationPopulated()
    {
        var yaml = readFixture("fonts_all_ops.yaml");
        var result = new YamlDeserializer().Deserialize(yaml);

        result.Fonts.Should().HaveCount(7);

        result.Fonts[0].Op.Should().Be("write_cidset");
        result.Fonts[0].Cids.Should().HaveCount(4);

        result.Fonts[1].Op.Should().Be("write_charset");
        result.Fonts[1].GlyphNames.Should().HaveCount(5);

        result.Fonts[2].Op.Should().Be("set_encoding");
        result.Fonts[2].Encoding.Should().Be("WinAnsiEncoding");

        result.Fonts[3].Op.Should().Be("set_differences");
        result.Fonts[3].Differences.Should().HaveCount(2);
        result.Fonts[3].Differences[128].Should().Be("Euro");

        result.Fonts[4].Op.Should().Be("write_tounicode");
        result.Fonts[4].Mappings.Should().HaveCount(3);
        result.Fonts[4].Mappings[65].Should().Be("A");

        result.Fonts[5].Op.Should().Be("set_widths");
        result.Fonts[5].Widths.Should().HaveCount(2);
        result.Fonts[5].Widths[42].Should().Be(600f);

        result.Fonts[6].Op.Should().Be("add_font_descriptor");
        result.Fonts[6].Font.Should().Be("TT1");
    }

    [Fact]
    public void EmptyString_Yaml_ThrowsMeaningfulException()
    {
        var act = () => new YamlDeserializer().Deserialize("");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void EmptyString_Json_ThrowsMeaningfulException()
    {
        var act = () => new JsonDeserializer().Deserialize("");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void UnknownTopLevelKey_Yaml_DoesNotThrow()
    {
        var yaml = "version: \"1.0\"\nunknown_key: \"ignored\"\n";
        var result = new YamlDeserializer().Deserialize(yaml);

        result.Version.Should().Be("1.0");
    }

    [Fact]
    public void InstructionDeserializer_DetectsYaml_ByContentType()
    {
        var yaml = readFixture("metadata_only.yaml");
        var deserializer = new InstructionDeserializer();
        var result = deserializer.Deserialize(yaml, "application/x-yaml");

        result.Metadata.Language.Should().Be("en-US");
    }

    [Fact]
    public void InstructionDeserializer_DetectsJson_ByContentType()
    {
        var json = readFixture("metadata_only.json");
        var deserializer = new InstructionDeserializer();
        var result = deserializer.Deserialize(json, "application/json");

        result.Metadata.Language.Should().Be("en-US");
    }

    [Fact]
    public void InstructionDeserializer_SniffsJson_WhenNoContentType()
    {
        var json = readFixture("metadata_only.json");
        var deserializer = new InstructionDeserializer();
        var result = deserializer.Deserialize(json);

        result.Metadata.Language.Should().Be("en-US");
    }

    [Fact]
    public void InstructionDeserializer_SniffsYaml_WhenNoContentType()
    {
        var yaml = readFixture("metadata_only.yaml");
        var deserializer = new InstructionDeserializer();
        var result = deserializer.Deserialize(yaml);

        result.Metadata.Language.Should().Be("en-US");
    }

    private static void assertFullInstruction(InstructionSet result)
    {
        result.Version.Should().Be("1.0");

        result.Metadata.Should().NotBeNull();
        result.Metadata.Language.Should().Be("en-US");
        result.Metadata.Title.Should().Be("Q4 Planning Meeting Agenda");
        result.Metadata.DisplayDocTitle.Should().BeTrue();
        result.Metadata.MarkInfo.Should().BeTrue();
        result.Metadata.PdfUaIdentifier.Should().Be(1);
        result.Metadata.TabOrder.Should().Be("structure");

        result.Structure.Should().NotBeNull();
        result.Structure.StripExisting.Should().BeTrue();
        result.Structure.Root.Should().Be("Document");
        result.Structure.Children.Should().HaveCount(5);
        result.Structure.Children[0].Id.Should().Be("heading");
        result.Structure.Children[0].Role.Should().Be("H1");
        result.Structure.Children[0].Bbox.Should().NotBeNull();
        result.Structure.Children[0].Bbox.X.Should().Be(72f);

        result.Structure.Children[2].Role.Should().Be("Figure");
        result.Structure.Children[2].AltText.Should().Be("Company Logo");

        var list = result.Structure.Children[3];
        list.Role.Should().Be("L");
        list.Children.Should().HaveCount(1);
        list.Children[0].Children.Should().HaveCount(2);
        list.Children[0].Children[0].ActualText.Should().Be("1.");

        var table = result.Structure.Children[4];
        table.Role.Should().Be("Table");
        var row = table.Children[0];
        row.Children[0].Scope.Should().Be("Column");
        row.Children[1].ColSpan.Should().Be(2);
        row.Children[1].Attributes.Should().HaveCount(1);

        result.ContentTagging.Should().HaveCount(2);
        result.ContentTagging[0].Node.Should().Be("heading");
        result.ContentTagging[0].Page.Should().Be(1);
        result.ContentTagging[0].Bbox.Width.Should().Be(468f);

        result.Artifacts.Should().HaveCount(1);
        result.Artifacts[0].Type.Should().Be("layout");

        result.Annotations.Should().HaveCount(2);
        result.Annotations[0].Op.Should().Be("set_contents");
        result.Annotations[1].Op.Should().Be("create_widget");
        result.Annotations[1].FieldName.Should().Be("last_name");
        result.Annotations[1].Tu.Should().Be("Last Name");

        result.Bookmarks.Should().NotBeNull();
        result.Bookmarks.GenerateFromHeadings.Should().BeTrue();

        result.Fonts.Should().HaveCount(2);
        result.Fonts[0].Op.Should().Be("write_cidset");
        result.Fonts[0].Cids.Should().HaveCount(6);
        result.Fonts[1].Op.Should().Be("write_tounicode");
        result.Fonts[1].Mappings[65].Should().Be("A");

        result.Ocr.Should().HaveCount(1);
        result.Ocr[0].Words.Should().HaveCount(1);
        result.Ocr[0].Words[0].Text.Should().Be("MEETING");
        result.Ocr[0].Words[0].Confidence.Should().Be(0.98f);
    }
}
