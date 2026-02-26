using System.Collections;
using System.IO.Compression;
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
        result.Fonts.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontHasCorrectName()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        result.Fonts.Should().Contain(f => f.Name.Contains("Helvetica"));
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontIsNotEmbedded()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        var helvetica = result.Fonts.First(f => f.Name.Contains("Helvetica"));
        helvetica.IsEmbedded.Should().BeFalse();
    }

    [Fact]
    public void Inspect_UntaggedSimple_FontHasEncoding()
    {
        var path = FixtureGenerator.EnsureUntaggedSimple();
        var result = inspectFixture(path);

        var helvetica = result.Fonts.First(f => f.Name.Contains("Helvetica"));
        helvetica.Encoding.Should().NotBeNull();
    }

    [Fact]
    public void Inspect_Scanned_FontsAreEmptyForImageOnlyPage()
    {
        var path = FixtureGenerator.EnsureScanned();
        var result = inspectFixture(path);

        result.Pages.Should().HaveCount(1);
        result.Fonts.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_HasTounicodeIsCorrectForCidFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var docFonts = result.Fonts;
        docFonts.Should().HaveCountGreaterThanOrEqualTo(2);

        // The embedded CID font should have a ToUnicode CMap
        var cidFont = docFonts.First(f => f.IsEmbedded);
        cidFont.HasTounicode.Should().BeTrue();
    }

    [Fact]
    public void Inspect_MixedFonts_IsEmbeddedCorrectForEachFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var docFonts = result.Fonts;

        // CID font is embedded
        docFonts.Should().Contain(f => f.IsEmbedded);
        // Helvetica is not embedded
        docFonts.Should().Contain(f => !f.IsEmbedded && f.Name.Contains("Helvetica"));
    }

    [Fact]
    public void Inspect_MixedFonts_TounicodeMappingsPopulatedForCidFont()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var cidFont = result.Fonts.First(f => f.IsEmbedded);
        cidFont.TounicodeMappings.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_MixedFonts_Type3InfoPopulated()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        result.Fonts.Should().NotBeEmpty();
        var type3Font = result.Fonts.First(f => f.FontType == "Type3");
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
        var type3Font = result.Fonts.First(f => f.FontType == "Type3");
        type3Font.UnmappableCharCodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Inspect_EmbeddedFont_FontProgramDataIsValidBase64GzippedBytes()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var embeddedFont = result.Fonts.First(f => f.IsEmbedded);
        var prop = typeof(DocumentFont).GetProperty("FontProgramData");
        prop.Should().NotBeNull("DocumentFont must have a FontProgramData property");

        var value = prop.GetValue(embeddedFont) as string;
        value.Should().NotBeNullOrEmpty();

        var compressedBytes = Convert.FromBase64String(value);
        using var ms = new MemoryStream(compressedBytes);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        output.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Inspect_NonEmbeddedFont_FontProgramDataIsNull()
    {
        var path = FixtureGenerator.EnsureMixedFonts();
        var result = inspectFixture(path);

        var nonEmbedded = result.Fonts.First(f => !f.IsEmbedded);
        var prop = typeof(DocumentFont).GetProperty("FontProgramData");
        prop.Should().NotBeNull("DocumentFont must have a FontProgramData property");

        var value = prop.GetValue(nonEmbedded);
        value.Should().BeNull();
    }

    [Fact]
    public void Inspect_MultipleEmbeddedFonts_EachHasDistinctFontProgramData()
    {
        var path = FixtureGenerator.EnsureMultipleEmbeddedFonts();
        var result = inspectFixture(path);

        var embeddedFonts = result.Fonts.Where(f => f.IsEmbedded).ToList();
        embeddedFonts.Should().HaveCountGreaterThanOrEqualTo(2);

        var prop = typeof(DocumentFont).GetProperty("FontProgramData");
        prop.Should().NotBeNull("DocumentFont must have a FontProgramData property");

        var values = embeddedFonts.Select(f => prop.GetValue(f) as string).ToList();
        values.Should().AllSatisfy(v => v.Should().NotBeNullOrEmpty());
        values.Distinct().Should().HaveCountGreaterThan(1,
            "different embedded fonts must have different font program bytes");
    }

    [Fact]
    public void FontInfo_DoesNotHave_FontProgramCids()
    {
        typeof(FontInfo).GetProperty("FontProgramCids").Should().BeNull();
    }

    [Fact]
    public void FontInfo_DoesNotHave_FontProgramWidths()
    {
        typeof(FontInfo).GetProperty("FontProgramWidths").Should().BeNull();
    }

    [Fact]
    public void FontInfo_DoesNotHave_DictionaryWidths()
    {
        typeof(FontInfo).GetProperty("DictionaryWidths").Should().BeNull();
    }

    [Fact]
    public void FontInfo_DoesNotHave_CharsetGlyphNames()
    {
        typeof(FontInfo).GetProperty("CharsetGlyphNames").Should().BeNull();
    }

    [Fact]
    public void FontInfo_DoesNotHave_FontProgramGlyphNames()
    {
        typeof(FontInfo).GetProperty("FontProgramGlyphNames").Should().BeNull();
    }

    [Fact]
    public void FontInfo_DoesNotHave_CidsetCids()
    {
        typeof(FontInfo).GetProperty("CidsetCids").Should().BeNull();
    }

    // --- Failing tests for new response shape (Prompt 2) ---

    [Fact]
    public void InspectResponse_HasTopLevelFontsProperty()
    {
        typeof(InspectResponse).GetProperty("Fonts").Should().NotBeNull(
            "InspectResponse must have a top-level Fonts array");
    }

    [Fact]
    public void DocumentFont_HasPagesIntArrayProperty()
    {
        var assembly = typeof(InspectResponse).Assembly;
        var documentFontType = assembly.GetTypes().FirstOrDefault(t => t.Name == "DocumentFont");
        documentFontType.Should().NotBeNull("DocumentFont class must exist in EffiTex.Engine");
        var pagesProp = documentFontType?.GetProperty("Pages");
        pagesProp.Should().NotBeNull("DocumentFont must have a Pages property");
        pagesProp?.PropertyType.Should().Be(typeof(int[]), "DocumentFont.Pages must be int[]");
    }

    [Fact]
    public void PageInfo_FontsIsListOfString()
    {
        typeof(PageInfo).GetProperty("Fonts").PropertyType
            .Should().Be(typeof(List<string>), "page-level Fonts must be font name strings only");
    }

    [Fact]
    public void Inspect_StructuredHeadings_SharedFontDeduplicatedToSingleDocumentEntry()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        var fontsProp = typeof(InspectResponse).GetProperty("Fonts");
        fontsProp.Should().NotBeNull("InspectResponse must have a top-level Fonts property");

        var docFonts = fontsProp.GetValue(result) as IList;
        docFonts.Should().NotBeNull();
        docFonts.Count.Should().Be(1,
            "Helvetica appears on both pages but must produce exactly one document-level entry");
    }

    [Fact]
    public void Inspect_MultiPageEmbeddedFont_FontProgramDataOnDocumentFont()
    {
        var path = FixtureGenerator.EnsureMultiPageEmbeddedFont();
        var result = inspectFixture(path);

        var fontsProp = typeof(InspectResponse).GetProperty("Fonts");
        fontsProp.Should().NotBeNull("InspectResponse must have a top-level Fonts property");

        var docFonts = fontsProp.GetValue(result) as IList;
        docFonts.Should().NotBeNull();
        docFonts.Count.Should().BeGreaterThan(0);

        var embeddedFont = docFonts.Cast<object>()
            .FirstOrDefault(f => (bool)(f.GetType().GetProperty("IsEmbedded")?.GetValue(f) ?? false));
        embeddedFont.Should().NotBeNull("at least one embedded font must be present at document level");

        var programDataProp = embeddedFont.GetType().GetProperty("FontProgramData");
        programDataProp.Should().NotBeNull("DocumentFont must have FontProgramData");
        (programDataProp.GetValue(embeddedFont) as string).Should().NotBeNullOrEmpty(
            "embedded font must have FontProgramData at document level");
    }

    [Fact]
    public void Inspect_StructuredHeadings_AllPageFontNamesHaveDocumentLevelEntry()
    {
        var path = FixtureGenerator.EnsureStructuredHeadings();
        var result = inspectFixture(path);

        var fontsProp = typeof(InspectResponse).GetProperty("Fonts");
        fontsProp.Should().NotBeNull("InspectResponse must have a top-level Fonts property");

        var docFonts = fontsProp.GetValue(result) as IList;
        docFonts.Should().NotBeNull();

        var docFontNames = new HashSet<string>();
        foreach (var font in docFonts)
        {
            var name = font.GetType().GetProperty("Name")?.GetValue(font) as string;
            if (name != null) docFontNames.Add(name);
        }

        var pageFontsProp = typeof(PageInfo).GetProperty("Fonts");
        pageFontsProp.PropertyType.Should().Be(typeof(List<string>),
            "PageInfo.Fonts must be List<string> after refactor");

        foreach (var page in result.Pages)
        {
            var pageFonts = pageFontsProp.GetValue(page) as List<string>;
            pageFonts.Should().NotBeNull();
            foreach (var fontName in pageFonts)
            {
                docFontNames.Should().Contain(fontName,
                    $"font '{fontName}' on page {page.PageNumber} must exist in document-level Fonts");
            }
        }
    }
}
