using EffiTex.Core.Models;

namespace EffiTex.Core.Deserialization;

public class InstructionDeserializer
{
    private readonly YamlDeserializer _yamlDeserializer = new();
    private readonly JsonDeserializer _jsonDeserializer = new();

    public InstructionSet Deserialize(string content, string contentType = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Instruction content cannot be empty.");
        }

        if (isJson(content, contentType))
        {
            return _jsonDeserializer.Deserialize(content);
        }

        return _yamlDeserializer.Deserialize(content);
    }

    private static bool isJson(string content, string contentType)
    {
        if (!string.IsNullOrEmpty(contentType))
        {
            return contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
        }

        var trimmed = content.TrimStart();
        return trimmed.StartsWith("{") || trimmed.StartsWith("[");
    }
}
