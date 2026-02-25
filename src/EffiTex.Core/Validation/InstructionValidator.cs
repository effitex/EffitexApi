using EffiTex.Core.Models;

namespace EffiTex.Core.Validation;

public class InstructionValidator
{
    private static readonly HashSet<string> VALID_TAB_ORDERS = new(StringComparer.OrdinalIgnoreCase)
    {
        "structure", "row", "column", "unordered"
    };

    private static readonly HashSet<string> VALID_ROLES = new(StringComparer.Ordinal)
    {
        "Document", "Part", "Art", "Sect", "Div", "BlockQuote", "Caption",
        "TOC", "TOCI", "Index", "NonStruct", "Private",
        "P", "H", "H1", "H2", "H3", "H4", "H5", "H6",
        "L", "LI", "Lbl", "LBody",
        "Table", "TR", "TH", "TD", "THead", "TBody", "TFoot",
        "Span", "Quote", "Note", "Reference", "BibEntry", "Code",
        "Link", "Annot", "Ruby", "RB", "RT", "RP",
        "Warichu", "WT", "WP",
        "Figure", "Formula", "Form"
    };

    private static readonly HashSet<string> VALID_SCOPES = new(StringComparer.Ordinal)
    {
        "Row", "Column", "Both"
    };

    private static readonly HashSet<string> VALID_ARTIFACT_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "layout", "header", "footer", "pagination", "background"
    };

    private static readonly HashSet<string> VALID_ANNOTATION_OPS = new(StringComparer.Ordinal)
    {
        "set_contents", "set_tu", "associate", "create_widget"
    };

    private static readonly HashSet<string> VALID_FONT_OPS = new(StringComparer.Ordinal)
    {
        "write_cidset", "write_charset", "set_encoding", "set_differences",
        "write_tounicode", "set_widths", "add_font_descriptor"
    };

    private static readonly HashSet<string> VALID_FIELD_TYPES = new(StringComparer.Ordinal)
    {
        "Tx", "Btn", "Ch"
    };

    public ValidationResult Validate(InstructionSet set)
    {
        var result = new ValidationResult();

        validateVersion(set, result);
        validateMetadata(set.Metadata, result);

        var structureIds = collectStructureIds(set.Structure);
        validateStructure(set.Structure, result);
        validateContentTagging(set.ContentTagging, structureIds, result);
        validateArtifacts(set.Artifacts, result);
        validateAnnotations(set.Annotations, structureIds, result);
        validateFonts(set.Fonts, result);
        validateOcr(set.Ocr, result);

        return result;
    }

    private static void validateVersion(InstructionSet set, ValidationResult result)
    {
        if (string.IsNullOrEmpty(set.Version))
        {
            result.Errors.Add(new ValidationError { Field = "version", Message = "Version is required." });
            return;
        }

        if (set.Version != "1.0")
        {
            result.Errors.Add(new ValidationError { Field = "version", Message = "Version must be \"1.0\"." });
        }
    }

    private static void validateMetadata(MetadataInstruction metadata, ValidationResult result)
    {
        if (metadata == null) return;

        if (metadata.Language != null && string.IsNullOrWhiteSpace(metadata.Language))
        {
            result.Errors.Add(new ValidationError { Field = "metadata.language", Message = "Language must not be empty." });
        }

        if (metadata.TabOrder != null && !VALID_TAB_ORDERS.Contains(metadata.TabOrder))
        {
            result.Errors.Add(new ValidationError { Field = "metadata.tab_order", Message = $"Invalid tab order: \"{metadata.TabOrder}\". Must be structure, row, column, or unordered." });
        }

        if (metadata.PdfUaIdentifier.HasValue && metadata.PdfUaIdentifier != 1 && metadata.PdfUaIdentifier != 2)
        {
            result.Errors.Add(new ValidationError { Field = "metadata.pdfua_identifier", Message = "PDF/UA identifier must be 1 or 2." });
        }
    }

    private static void validateStructure(StructureInstruction structure, ValidationResult result)
    {
        if (structure == null) return;

        if (string.IsNullOrEmpty(structure.Root))
        {
            result.Errors.Add(new ValidationError { Field = "structure.root", Message = "Root is required." });
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        if (structure.Children != null)
        {
            validateStructureNodes(structure.Children, "structure.children", seenIds, result);
        }
    }

    private static void validateStructureNodes(List<StructureNode> nodes, string path, HashSet<string> seenIds, ValidationResult result)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var nodePath = $"{path}[{i}]";

            if (!string.IsNullOrEmpty(node.Role) && !VALID_ROLES.Contains(node.Role))
            {
                result.Errors.Add(new ValidationError { Field = $"{nodePath}.role", Message = $"Invalid role: \"{node.Role}\"." });
            }

            if (!string.IsNullOrEmpty(node.Id))
            {
                if (!seenIds.Add(node.Id))
                {
                    result.Errors.Add(new ValidationError { Field = $"{nodePath}.id", Message = $"Duplicate id: \"{node.Id}\"." });
                }
            }

            if (node.Scope != null && !VALID_SCOPES.Contains(node.Scope))
            {
                result.Errors.Add(new ValidationError { Field = $"{nodePath}.scope", Message = $"Invalid scope: \"{node.Scope}\". Must be Row, Column, or Both." });
            }

            if (node.ColSpan.HasValue && node.ColSpan.Value <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{nodePath}.colspan", Message = "ColSpan must be a positive integer." });
            }

            if (node.RowSpan.HasValue && node.RowSpan.Value <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{nodePath}.rowspan", Message = "RowSpan must be a positive integer." });
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                validateStructureNodes(node.Children, $"{nodePath}.children", seenIds, result);
            }
        }
    }

    private static HashSet<string> collectStructureIds(StructureInstruction structure)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (structure?.Children == null) return ids;

        collectIds(structure.Children, ids);
        return ids;
    }

    private static void collectIds(List<StructureNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrEmpty(node.Id))
            {
                ids.Add(node.Id);
            }

            if (node.Children != null)
            {
                collectIds(node.Children, ids);
            }
        }
    }

    private static void validateContentTagging(List<ContentTaggingEntry> entries, HashSet<string> structureIds, ValidationResult result)
    {
        if (entries == null) return;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var path = $"content_tagging[{i}]";

            if (!string.IsNullOrEmpty(entry.Node) && !structureIds.Contains(entry.Node))
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.node", Message = $"Node \"{entry.Node}\" does not reference a valid structure node id." });
            }

            if (entry.Page <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.page", Message = "Page must be a positive integer." });
            }

            validateBbox(entry.Bbox, $"{path}.bbox", result);
        }
    }

    private static void validateBbox(BoundingBox bbox, string path, ValidationResult result)
    {
        if (bbox == null)
        {
            result.Errors.Add(new ValidationError { Field = path, Message = "Bbox is required." });
            return;
        }

        if (bbox.X < 0 || bbox.Y < 0)
        {
            result.Errors.Add(new ValidationError { Field = path, Message = "Bbox x and y must be non-negative." });
        }

        if (bbox.Width <= 0 || bbox.Height <= 0)
        {
            result.Errors.Add(new ValidationError { Field = path, Message = "Bbox width and height must be positive." });
        }
    }

    private static void validateArtifacts(List<ArtifactEntry> entries, ValidationResult result)
    {
        if (entries == null) return;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var path = $"artifacts[{i}]";

            if (entry.Page <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.page", Message = "Page must be a positive integer." });
            }

            validateBbox(entry.Bbox, $"{path}.bbox", result);

            if (!VALID_ARTIFACT_TYPES.Contains(entry.Type ?? ""))
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.type", Message = $"Invalid artifact type: \"{entry.Type}\"." });
            }
        }
    }

    private static void validateAnnotations(List<AnnotationOperation> ops, HashSet<string> structureIds, ValidationResult result)
    {
        if (ops == null) return;

        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            var path = $"annotations[{i}]";

            if (!VALID_ANNOTATION_OPS.Contains(op.Op ?? ""))
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.op", Message = $"Invalid annotation op: \"{op.Op}\"." });
                continue;
            }

            if (op.Page <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.page", Message = "Page must be a positive integer." });
            }

            switch (op.Op)
            {
                case "set_contents":
                case "set_tu":
                    if (!op.Index.HasValue)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.index", Message = "Index is required for set_contents/set_tu." });
                    }
                    if (string.IsNullOrEmpty(op.Value))
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.value", Message = "Value is required for set_contents/set_tu." });
                    }
                    break;

                case "associate":
                    if (!op.Index.HasValue)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.index", Message = "Index is required for associate." });
                    }
                    if (!string.IsNullOrEmpty(op.Node) && !structureIds.Contains(op.Node))
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.node", Message = $"Node \"{op.Node}\" does not reference a valid structure node id." });
                    }
                    break;

                case "create_widget":
                    if (op.Rect == null)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.rect", Message = "Rect is required for create_widget." });
                    }
                    if (string.IsNullOrEmpty(op.FieldName))
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.field_name", Message = "FieldName is required for create_widget." });
                    }
                    if (!VALID_FIELD_TYPES.Contains(op.FieldType ?? ""))
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.field_type", Message = $"Invalid field type: \"{op.FieldType}\". Must be Tx, Btn, or Ch." });
                    }
                    break;
            }
        }
    }

    private static void validateFonts(List<FontOperation> ops, ValidationResult result)
    {
        if (ops == null) return;

        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            var path = $"fonts[{i}]";

            if (!VALID_FONT_OPS.Contains(op.Op ?? ""))
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.op", Message = $"Invalid font op: \"{op.Op}\"." });
                continue;
            }

            if (string.IsNullOrEmpty(op.Font))
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.font", Message = "Font name is required." });
            }

            if (op.Page <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.page", Message = "Page must be a positive integer." });
            }

            switch (op.Op)
            {
                case "write_cidset":
                    if (op.Cids == null || op.Cids.Count == 0)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.cids", Message = "Cids are required for write_cidset." });
                    }
                    break;
                case "write_charset":
                    if (op.GlyphNames == null || op.GlyphNames.Count == 0)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.glyph_names", Message = "GlyphNames are required for write_charset." });
                    }
                    break;
                case "set_encoding":
                    if (string.IsNullOrEmpty(op.Encoding))
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.encoding", Message = "Encoding is required for set_encoding." });
                    }
                    break;
                case "set_differences":
                    if (op.Differences == null || op.Differences.Count == 0)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.differences", Message = "Differences are required for set_differences." });
                    }
                    break;
                case "write_tounicode":
                    if (op.Mappings == null || op.Mappings.Count == 0)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.mappings", Message = "Mappings are required for write_tounicode." });
                    }
                    break;
                case "set_widths":
                    if (op.Widths == null || op.Widths.Count == 0)
                    {
                        result.Errors.Add(new ValidationError { Field = $"{path}.widths", Message = "Widths are required for set_widths." });
                    }
                    break;
            }
        }
    }

    private static void validateOcr(List<OcrPage> pages, ValidationResult result)
    {
        if (pages == null) return;

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var path = $"ocr[{i}]";

            if (page.Page <= 0)
            {
                result.Errors.Add(new ValidationError { Field = $"{path}.page", Message = "Page must be a positive integer." });
            }

            if (page.Words == null) continue;

            for (var j = 0; j < page.Words.Count; j++)
            {
                var word = page.Words[j];
                var wordPath = $"{path}.words[{j}]";

                if (string.IsNullOrEmpty(word.Text))
                {
                    result.Errors.Add(new ValidationError { Field = $"{wordPath}.text", Message = "Text is required." });
                }

                validateBbox(word.Bbox, $"{wordPath}.bbox", result);

                if (word.Confidence.HasValue && (word.Confidence < 0f || word.Confidence > 1f))
                {
                    result.Errors.Add(new ValidationError { Field = $"{wordPath}.confidence", Message = "Confidence must be between 0.0 and 1.0." });
                }
            }
        }
    }
}
