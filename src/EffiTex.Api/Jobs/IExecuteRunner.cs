using EffiTex.Core.Models;

namespace EffiTex.Api.Jobs;

public interface IExecuteRunner
{
    Stream Execute(Stream inputPdf, InstructionSet instructions);
}
