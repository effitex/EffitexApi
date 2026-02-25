using EffiTex.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EffiTex.Core.Deserialization;

public class YamlDeserializer
{
    private readonly IDeserializer _deserializer;

    public YamlDeserializer()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(new DslNamingConvention())
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public InstructionSet Deserialize(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("YAML content cannot be empty.");
        }

        return _deserializer.Deserialize<InstructionSet>(yaml);
    }
}
