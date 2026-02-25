using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EffiTex.Functions.Tests.Helpers;

public class MockHttpResponseData : HttpResponseData
{
    public MockHttpResponseData(FunctionContext context) : base(context)
    {
        Body = new MemoryStream();
        Headers = new HttpHeadersCollection();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies => throw new NotImplementedException();

    public string ReadBody()
    {
        Body.Position = 0;
        using var reader = new StreamReader(Body);
        return reader.ReadToEnd();
    }
}
