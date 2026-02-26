using EffiTex.Core.Models;
using EffiTex.Engine;

namespace EffiTex.Api.Jobs;

public class ExecuteRunner : IExecuteRunner
{
    private readonly Interpreter _interpreter;

    public ExecuteRunner(Interpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public Stream Execute(Stream inputPdf, InstructionSet instructions) =>
        _interpreter.Execute(inputPdf, instructions);
}
