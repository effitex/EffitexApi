using EffiTex.Core.Models;

namespace EffiTex.Functions;

public interface IJobProcessor
{
    Stream Execute(Stream inputPdf, InstructionSet instructions);
}
