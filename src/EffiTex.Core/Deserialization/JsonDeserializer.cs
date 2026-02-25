using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EffiTex.Core.Models;

namespace EffiTex.Core.Deserialization;

public class JsonDeserializer
{
    private readonly JsonSerializerOptions _options;

    public JsonDeserializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DslJsonNamingPolicy(),
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new IntKeyDictionaryConverterFactory() }
        };
    }

    public InstructionSet Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be empty.");
        }

        return JsonSerializer.Deserialize<InstructionSet>(json, _options);
    }
}

internal class DslJsonNamingPolicy : JsonNamingPolicy
{
    private static readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal)
    {
        ["PdfUaIdentifier"] = "pdfua_identifier",
        ["ColSpan"] = "colspan",
        ["RowSpan"] = "rowspan",
    };

    public override string ConvertName(string name)
    {
        if (_overrides.TryGetValue(name, out var mapped))
        {
            return mapped;
        }

        return JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
    }
}

internal class IntKeyDictionaryConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        var generic = typeToConvert.GetGenericTypeDefinition();
        if (generic != typeof(Dictionary<,>))
            return false;

        return typeToConvert.GetGenericArguments()[0] == typeof(int);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[1];
        var converterType = typeof(IntKeyDictionaryConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType);
    }
}

internal class IntKeyDictionaryConverter<TValue> : JsonConverter<Dictionary<int, TValue>>
{
    public override Dictionary<int, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for dictionary.");

        var dict = new Dictionary<int, TValue>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dict;

            var key = int.Parse(reader.GetString());
            reader.Read();
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            dict[key] = value;
        }

        throw new JsonException("Unexpected end of JSON.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<int, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key.ToString());
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}
