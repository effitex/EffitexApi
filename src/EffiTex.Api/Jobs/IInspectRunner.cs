using EffiTex.Engine.Models.Inspect;

namespace EffiTex.Api.Jobs;

public interface IInspectRunner
{
    InspectResponse Inspect(Stream pdfStream);
}
