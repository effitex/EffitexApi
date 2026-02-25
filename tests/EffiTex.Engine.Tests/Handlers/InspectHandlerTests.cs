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

    [Fact]
    public void Inspect_UntaggedSimple_FontsArePopulated()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Fonts.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontHasCorrectName()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        var fonts = result.Pages[0].Fonts;
        fonts.Should().Contain(f => f.Name.Contains("Helvetica"));
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontIsNotEmbedded()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        var helvetica = result.Pages[0].Fonts
            .First(f => f.Name.Contains("Helvetica"));

        helvetica.IsEmbedded.Should().BeFalse();
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontHasEncoding()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        var helvetica = result.Pages[0].Fonts
            .First(f => f.Name.Contains("Helvetica"));

        helvetica.Encoding.Should().NotBeNull();
    }

    [Fact]
    public void Inspect_Scanned_FontsAreEmptyForImageOnlyPage()
    {
        var path = FixtureGenerator.EnsureScanned();
        var result = inspectFixture(path);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Fonts.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_HasTounicodeIsCorrectForCidFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        // Page 1 has CIDFontType2 (embedded) and Type1 (Helvetica)
        var page1Fonts = result.Pages[0].Fonts;
        page1Fonts.Should().HaveCountGreaterThanOrEqualTo(2);

        // The embedded CID font should have a ToUnicode CMap
        var cidFont = page1Fonts.First(f => f.IsEmbedded);
        cidFont.HasTounicode.Should().BeTrue();
    }

    [Fact]
    public void Inspect_MixedFonts_IsEmbeddedCorrectForEachFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var page1Fonts = result.Pages[0].Fonts;

        // CID font is embedded
        page1Fonts.Should().Contain(f => f.IsEmbedded);
        // Helvetica is not embedded
        page1Fonts.Should().Contain(f => !f.IsEmbedded && f.Name.Contains("Helvetica"));
    }

    [Fact]
    public void Inspect_MixedFonts_TounicodeMappingsPopulatedForCidFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var cidFont = result.Pages[0].Fonts.First(f => f.IsEmbedded);
        cidFont.TounicodeMappings.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_FontProgramCidsPopulatedFromBinary()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var cidFont = result.Pages[0].Fonts.First(f => f.IsEmbedded);
        cidFont.FontProgramCids.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_Type3InfoPopulated()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        // Page 2 has the Type3 font
        var page2Fonts = result.Pages[1].Fonts;
        page2Fonts.Should().NotBeEmpty();

        var type3Font = page2Fonts.First(f => f.FontType == "Type3");
        type3Font.Type3Info.Should().NotBeNull();
        type3Font.Type3Info.CharProcsGlyphNames.Should().Contain("glyph0");
        type3Font.Type3Info.CharProcsGlyphNames.Should().Contain("glyph1");
        type3Font.Type3Info.CharProcsGlyphNames.Should().Contain("glyph2");
    }

    [Fact]
    public void Inspect_MixedFonts_UnmappableCharCodesNonEmptyForType3()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        // Type3 font with non-AGL glyph names and no ToUnicode → unmappable
        var type3Font = result.Pages[1].Fonts.First(f => f.FontType == "Type3");
        type3Font.UnmappableCharCodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_CidsetCidsPopulatedForCidFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var cidFont = result.Pages[0].Fonts.First(f => f.IsEmbedded);
        // CIDSet might or might not be added by iText; test that when present, it's parsed
        if (cidFont.HasCidset)
        {
            cidFont.CidsetCids.Should().NotBeEmpty();
        }
    }
}
