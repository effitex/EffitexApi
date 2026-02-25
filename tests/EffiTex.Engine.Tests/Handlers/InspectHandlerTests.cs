using EffiTex.Engine;
using EffiTex.Engine.Models.Inspect;
using FluentAssertions;
using Xunit;

namespace EffiTex.Engine.Tests.Handlers;

public class InspectHandlerTests
{
    private readonly InspectHandler _handler = new();

    private InspectResponse inspectFixture(string path)
    {
        using var stream = File.OpenRead(path);
        return _handler.Inspect(stream);
    }

    [Fact]
    public void Inspect_UntaggedSimple_FileHashIsValid64CharHex()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.FileHash.Should().NotBeNullOrEmpty();
        result.FileHash.Should().HaveLength(64);
        result.FileHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Inspect_UntaggedSimple_PageCountMatchesKnownValue()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Document.PageCount.Should().Be(1);
    }

    [Fact]
    public void Inspect_UntaggedSimple_IsTaggedIsFalse()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Document.IsTagged.Should().BeFalse();
    }

    [Fact]
    public void Inspect_StructuredHeadings_IsTaggedIsTrue()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        result.Document.IsTagged.Should().BeTrue();
    }

    [Fact]
    public void Inspect_StructuredHeadings_StructureTreeIsPopulated()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        result.StructureTree.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_StructuredHeadings_StructureTreeNestingMatchesTagTree()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        // Root should be Document
        result.StructureTree.Should().HaveCount(1);
        var doc = result.StructureTree[0];
        doc.Role.Should().Be("Document");

        // Document has 4 children: H1, H2, P, H3
        doc.Children.Should().HaveCount(4);
        doc.Children[0].Role.Should().Be("H1");
        doc.Children[1].Role.Should().Be("H2");
        doc.Children[2].Role.Should().Be("P");
        doc.Children[3].Role.Should().Be("H3");
    }

    [Fact]
    public void Inspect_UntaggedSimple_LanguageIsNull()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Document.Language.Should().BeNull();
    }

    [Fact]
    public void Inspect_UntaggedSimple_XmpMetadataIsNull()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.XmpMetadata.Should().BeNull();
    }

    [Fact]
    public void Inspect_UntaggedSimple_PagesHaveCorrectDimensions()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Pages.Should().HaveCount(1);
        var page = result.Pages[0];
        page.PageNumber.Should().Be(1);
        page.Width.Should().Be(612f);
        page.Height.Should().Be(792f);
    }

    [Fact]
    public void Inspect_StructuredHeadings_PageCountMatchesMultiPage()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        result.Document.PageCount.Should().Be(2);
        result.Pages.Should().HaveCount(2);
    }

    [Fact]
    public void Inspect_StructuredHeadings_PagesHaveCorrectDimensions()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        foreach (var page in result.Pages)
        {
            page.Width.Should().Be(612f);
            page.Height.Should().Be(792f);
        }
    }

    [Fact]
    public void Inspect_Scanned_IsTaggedIsFalse()
    {
        var path = FixtureGenerator.EnsureScanned();
        var result = inspectFixture(path);

        result.Document.IsTagged.Should().BeFalse();
    }

    [Fact]
    public void Inspect_Scanned_StructureTreeIsEmpty()
    {
        var path = FixtureGenerator.EnsureScanned();
        var result = inspectFixture(path);

        result.StructureTree.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_Annotated_PagesHaveCorrectDimensions()
    {
        var path = FixtureGenerator.EnsureAnnotated();
        var result = inspectFixture(path);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Width.Should().Be(612f);
        result.Pages[0].Height.Should().Be(792f);
    }

    [Fact]
    public void Inspect_StructuredHeadings_StructuredMcidsListIsPresent()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        // Both pages should have a non-null StructuredMcids list
        result.Pages[0].StructuredMcids.Should().NotBeNull();
        result.Pages[1].StructuredMcids.Should().NotBeNull();
    }

    [Fact]
    public void Inspect_UntaggedSimple_FileSizeBytesIsPositive()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.FileSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Inspect_UntaggedSimple_RoleMapIsEmpty()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.RoleMap.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_OutlinesAreEmpty()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Outlines.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_EmbeddedFilesAreEmpty()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.EmbeddedFiles.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_OcgConfigurationsAreEmpty()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.OcgConfigurations.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_PdfVersionIsPresent()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Document.PdfVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_IsEncryptedIsFalse()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Document.IsEncrypted.Should().BeFalse();
        result.Document.EncryptionPermissions.Should().BeNull();
    }
}
