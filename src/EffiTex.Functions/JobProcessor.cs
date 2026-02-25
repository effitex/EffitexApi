using EffiTex.Core.Models;
using EffiTex.Engine;

namespace EffiTex.Functions;

public class JobProcessor : IJobProcessor
{
    private readonly Interpreter _interpreter;

    public JobProcessor(Interpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public Stream Execute(Stream inputPdf, InstructionSet instructions)
    {
        return _interpreter.Execute(inputPdf, instructions);
    }
}
