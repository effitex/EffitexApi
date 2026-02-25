using System.Text;
using YamlDotNet.Serialization;

namespace EffiTex.Core.Deserialization;

internal class DslNamingConvention : INamingConvention
{
    private static readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal)
    {
        ["PdfUaIdentifier"] = "pdfua_identifier",
        ["ColSpan"] = "colspan",
        ["RowSpan"] = "rowspan",
        ["DisplayDocTitle"] = "display_doc_title",
        ["ContentTagging"] = "content_tagging",
        ["AltText"] = "alt_text",
        ["ActualText"] = "actual_text",
        ["ElementId"] = "element_id",
        ["MarkInfo"] = "mark_info",
        ["TabOrder"] = "tab_order",
        ["StripExisting"] = "strip_existing",
        ["FieldName"] = "field_name",
        ["FieldType"] = "field_type",
        ["GlyphNames"] = "glyph_names",
        ["GenerateFromHeadings"] = "generate_from_headings",
    };

    public string Apply(string value)
    {
        if (_overrides.TryGetValue(value, out var mapped))
        {
            return mapped;
        }

        return toSnakeCase(value);
    }

    public string Reverse(string value)
    {
        return value;
    }

    private static string toSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
