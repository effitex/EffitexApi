using EffiTex.Engine;
using EffiTex.Engine.Models.Inspect;

namespace EffiTex.Api.Jobs;

public class InspectRunner : IInspectRunner
{
    private readonly InspectHandler _handler;

    public InspectRunner(InspectHandler handler)
    {
        _handler = handler;
    }

    public InspectResponse Inspect(Stream pdfStream) => _handler.Inspect(pdfStream);
}
