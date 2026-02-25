using EffiTex.Core.Models;
using EffiTex.Core.Validation;
using FluentAssertions;
using Xunit;

namespace EffiTex.Core.Tests.Validation;

public class ValidationTests
{
    private readonly InstructionValidator _validator = new();

    [Fact]
    public void FullyValid_InstructionSet_ReturnsIsValid()
    {
        var set = createValidInstructionSet();

        var result = _validator.Validate(set);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VersionOnly_NoSections_IsValid()
    {
        var set = new InstructionSet { Version = "1.0" };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Version_Missing_ProducesError()
    {
        var set = new InstructionSet { Version = null };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "version");
    }

    [Fact]
    public void Version_Invalid_ProducesError()
    {
        var set = new InstructionSet { Version = "2.0" };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "version");
    }

    [Fact]
    public void Metadata_InvalidLanguage_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction { Language = "" }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "metadata.language");
    }

    [Fact]
    public void Metadata_ValidLanguageTwoLetter_IsValid()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction { Language = "en" }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Metadata_InvalidTabOrder_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction { TabOrder = "invalid" }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "metadata.tab_order");
    }

    [Fact]
    public void Metadata_InvalidPdfUaIdentifier_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction { PdfUaIdentifier = 3 }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "metadata.pdfua_identifier");
    }

    [Fact]
    public void Structure_MissingRoot_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction { Root = null }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "structure.root");
    }

    [Fact]
    public void Structure_InvalidRole_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Role = "InvalidRole" }
                }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("role"));
    }

    [Fact]
    public void Structure_DuplicateIds_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Id = "dup", Role = "P" },
                    new() { Id = "dup", Role = "P" }
                }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("id"));
    }

    [Fact]
    public void Structure_InvalidScope_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Role = "TH", Scope = "Invalid" }
                }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("scope"));
    }

    [Fact]
    public void Structure_NegativeColSpan_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Role = "TD", ColSpan = 0 }
                }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("colspan"));
    }

    [Fact]
    public void ContentTagging_InvalidNodeRef_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Id = "h1", Role = "H1" }
                }
            },
            ContentTagging = new List<ContentTaggingEntry>
            {
                new() { Node = "nonexistent", Page = 1, Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("content_tagging"));
    }

    [Fact]
    public void ContentTagging_InvalidPage_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode> { new() { Id = "h1", Role = "H1" } }
            },
            ContentTagging = new List<ContentTaggingEntry>
            {
                new() { Node = "h1", Page = 0, Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("page"));
    }

    [Fact]
    public void ContentTagging_ZeroWidthBbox_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode> { new() { Id = "h1", Role = "H1" } }
            },
            ContentTagging = new List<ContentTaggingEntry>
            {
                new() { Node = "h1", Page = 1, Bbox = new BoundingBox { X = 0, Y = 0, Width = 0, Height = 10 } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("bbox"));
    }

    [Fact]
    public void Artifacts_InvalidType_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Artifacts = new List<ArtifactEntry>
            {
                new() { Page = 1, Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 }, Type = "invalid" }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("type"));
    }

    [Fact]
    public void Annotations_InvalidOp_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Annotations = new List<AnnotationOperation>
            {
                new() { Op = "invalid_op", Page = 1 }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("op"));
    }

    [Fact]
    public void Annotations_SetContents_MissingIndex_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Annotations = new List<AnnotationOperation>
            {
                new() { Op = "set_contents", Page = 1, Value = "test" }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("index"));
    }

    [Fact]
    public void Annotations_SetContents_MissingValue_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Annotations = new List<AnnotationOperation>
            {
                new() { Op = "set_contents", Page = 1, Index = 0 }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("value"));
    }

    [Fact]
    public void Annotations_CreateWidget_MissingRect_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Annotations = new List<AnnotationOperation>
            {
                new() { Op = "create_widget", Page = 1, FieldName = "f1", FieldType = "Tx" }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("rect"));
    }

    [Fact]
    public void Annotations_CreateWidget_InvalidFieldType_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Annotations = new List<AnnotationOperation>
            {
                new()
                {
                    Op = "create_widget", Page = 1,
                    Rect = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 },
                    FieldName = "f1", FieldType = "Invalid"
                }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("field_type"));
    }

    [Fact]
    public void Fonts_InvalidOp_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Fonts = new List<FontOperation>
            {
                new() { Op = "invalid_op", Font = "F1", Page = 1 }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("op"));
    }

    [Fact]
    public void Fonts_WriteCidset_MissingCids_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Fonts = new List<FontOperation>
            {
                new() { Op = "write_cidset", Font = "F1", Page = 1 }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("cids"));
    }

    [Fact]
    public void Fonts_MissingFont_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Fonts = new List<FontOperation>
            {
                new() { Op = "write_cidset", Font = "", Page = 1, Cids = new List<int> { 0 } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("font"));
    }

    [Fact]
    public void Ocr_InvalidPage_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Ocr = new List<OcrPage>
            {
                new() { Page = 0, Words = new List<OcrWord> { new() { Text = "hi", Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 } } } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("page"));
    }

    [Fact]
    public void Ocr_EmptyText_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Ocr = new List<OcrPage>
            {
                new() { Page = 1, Words = new List<OcrWord> { new() { Text = "", Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 } } } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("text"));
    }

    [Fact]
    public void Ocr_InvalidConfidence_ProducesError()
    {
        var set = new InstructionSet
        {
            Version = "1.0",
            Ocr = new List<OcrPage>
            {
                new() { Page = 1, Words = new List<OcrWord> { new() { Text = "hi", Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 }, Confidence = 1.5f } } }
            }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field.Contains("confidence"));
    }

    [Fact]
    public void MultipleViolations_AllCollected_NotShortCircuited()
    {
        var set = new InstructionSet
        {
            Version = "2.0",
            Metadata = new MetadataInstruction { Language = "", TabOrder = "invalid", PdfUaIdentifier = 99 },
            Structure = new StructureInstruction { Root = null }
        };

        var result = _validator.Validate(set);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(3);
    }

    private InstructionSet createValidInstructionSet()
    {
        return new InstructionSet
        {
            Version = "1.0",
            Metadata = new MetadataInstruction
            {
                Language = "en-US",
                Title = "Test",
                DisplayDocTitle = true,
                MarkInfo = true,
                PdfUaIdentifier = 1,
                TabOrder = "structure"
            },
            Structure = new StructureInstruction
            {
                Root = "Document",
                Children = new List<StructureNode>
                {
                    new() { Id = "h1", Role = "H1" },
                    new() { Id = "p1", Role = "P" }
                }
            },
            ContentTagging = new List<ContentTaggingEntry>
            {
                new() { Node = "h1", Page = 1, Bbox = new BoundingBox { X = 72, Y = 700, Width = 468, Height = 24 } }
            },
            Artifacts = new List<ArtifactEntry>
            {
                new() { Page = 1, Bbox = new BoundingBox { X = 0, Y = 0, Width = 612, Height = 42 }, Type = "layout" }
            },
            Annotations = new List<AnnotationOperation>
            {
                new() { Op = "set_contents", Page = 1, Index = 0, Value = "test" }
            },
            Bookmarks = new BookmarksInstruction { GenerateFromHeadings = true },
            Fonts = new List<FontOperation>
            {
                new() { Op = "write_cidset", Font = "F1", Page = 1, Cids = new List<int> { 0, 1 } }
            },
            Ocr = new List<OcrPage>
            {
                new() { Page = 1, Words = new List<OcrWord> { new() { Text = "hi", Bbox = new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10 }, Confidence = 0.9f } } }
            }
        };
    }
}
