using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EffiTex.Functions.Tests.Helpers;

public class MockHttpRequestData : HttpRequestData
{
    private readonly Stream _body;
    private readonly HttpHeadersCollection _headers;
    private readonly Uri _url;
    private readonly string _method;

    public MockHttpRequestData(
        FunctionContext context,
        string body = "",
        string contentType = "application/x-yaml")
        : base(context)
    {
        _body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        _headers = new HttpHeadersCollection();
        _url = new Uri("https://localhost/api/test");
        _method = "POST";
        if (!string.IsNullOrEmpty(contentType))
        {
            _headers.Add("Content-Type", contentType);
        }
    }

    public MockHttpRequestData(FunctionContext context, Uri url, byte[] body)
        : base(context)
    {
        _url = url;
        _body = new MemoryStream(body);
        _headers = new HttpHeadersCollection();
        _method = "POST";
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers => _headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies => new List<IHttpCookie>();
    public override Uri Url => _url;
    public override IEnumerable<ClaimsIdentity> Identities => new List<ClaimsIdentity>();
    public override string Method => _method;

    public override HttpResponseData CreateResponse()
    {
        return new MockHttpResponseData(FunctionContext);
    }
}
