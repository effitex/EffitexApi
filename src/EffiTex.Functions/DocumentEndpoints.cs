using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using EffiTex.Engine;
using EffiTex.Engine.Models.Inspect;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EffiTex.Functions;

public class DocumentEndpoints
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly InspectHandler _inspectHandler;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DocumentEndpoints(BlobServiceClient blobServiceClient, InspectHandler inspectHandler)
    {
        _blobServiceClient = blobServiceClient;
        _inspectHandler = inspectHandler;
    }

    [Function("UploadDocument")]
    public async Task<HttpResponseData> UploadDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents")] HttpRequestData req)
    {
        // Parse multipart/form-data to extract the "file" field
        var boundary = GetBoundary(req.Headers);
        if (boundary == null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Missing or invalid Content-Type. Expected multipart/form-data.");
            return badRequest;
        }

        var fileBytes = await ExtractFileFromMultipart(req.Body, boundary);
        if (fileBytes == null || fileBytes.Length == 0)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("No file field found in the multipart form data.");
            return badRequest;
        }

        var guid = Guid.NewGuid().ToString();
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient($"{guid}/source.pdf");
        using var uploadStream = new MemoryStream(fileBytes);
        await blobClient.UploadAsync(uploadStream, overwrite: true);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { guid }, JsonOptions));
        return response;
    }

    [Function("InspectDocument")]
    public async Task<HttpResponseData> InspectDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/{guid}/inspect")] HttpRequestData req,
        string guid)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");
        var blobClient = containerClient.GetBlobClient($"{guid}/source.pdf");

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Source PDF not found.");
            return notFound;
        }

        using var pdfStream = new MemoryStream();
        await blobClient.DownloadToAsync(pdfStream);
        pdfStream.Position = 0;

        var inspectResult = _inspectHandler.Inspect(pdfStream);

        // Apply optional ?pages= filter
        var pagesParam = GetQueryParam(req.Url, "pages");
        if (!string.IsNullOrEmpty(pagesParam) && inspectResult.Pages != null)
        {
            var pageRange = ParsePageRange(pagesParam);
            if (pageRange != null)
            {
                inspectResult.Pages = inspectResult.Pages
                    .Where(p => p.PageNumber >= pageRange.Value.start && p.PageNumber <= pageRange.Value.end)
                    .ToList();
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var json = JsonSerializer.Serialize(inspectResult, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    [Function("GetPageImage")]
    public async Task<HttpResponseData> GetPageImage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/{guid}/pages/{p:int}/images/{idx:int}")] HttpRequestData req,
        string guid,
        int p,
        int idx)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");
        var blobClient = containerClient.GetBlobClient($"{guid}/source.pdf");

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Source PDF not found.");
            return notFound;
        }

        using var pdfStream = new MemoryStream();
        await blobClient.DownloadToAsync(pdfStream);
        pdfStream.Position = 0;

        var imageBytes = _inspectHandler.GetPageImage(pdfStream, p, idx);
        if (imageBytes == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Image not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        var contentType = DetectImageContentType(imageBytes);
        response.Headers.Add("Content-Type", contentType);
        await response.Body.WriteAsync(imageBytes, 0, imageBytes.Length);
        return response;
    }

    [Function("GetFigureInfo")]
    public async Task<HttpResponseData> GetFigureInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/{guid}/pages/{p:int}/figures/{mcid:int}")] HttpRequestData req,
        string guid,
        int p,
        int mcid)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");
        var blobClient = containerClient.GetBlobClient($"{guid}/source.pdf");

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Source PDF not found.");
            return notFound;
        }

        using var pdfStream = new MemoryStream();
        await blobClient.DownloadToAsync(pdfStream);
        pdfStream.Position = 0;

        var figureInfo = _inspectHandler.GetFigureInfo(pdfStream, p, mcid);
        if (figureInfo == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Figure info not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var json = JsonSerializer.Serialize(figureInfo, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    [Function("GetResult")]
    public async Task<HttpResponseData> GetResult(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/{guid}/result")] HttpRequestData req,
        string guid)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");
        var blobClient = containerClient.GetBlobClient($"{guid}/result.pdf");

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Result PDF not found.");
            return notFound;
        }

        using var resultStream = new MemoryStream();
        await blobClient.DownloadToAsync(resultStream);
        resultStream.Position = 0;

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/pdf");
        await resultStream.CopyToAsync(response.Body);
        return response;
    }

    private static string GetBoundary(HttpHeadersCollection headers)
    {
        if (!headers.TryGetValues("Content-Type", out var contentTypeValues))
            return null;

        var contentType = contentTypeValues.FirstOrDefault();
        if (contentType == null || !contentType.Contains("multipart/form-data"))
            return null;

        var boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
        if (boundaryIndex < 0)
            return null;

        var boundary = contentType.Substring(boundaryIndex + "boundary=".Length).Trim();
        // Remove quotes if present
        if (boundary.StartsWith("\"") && boundary.EndsWith("\""))
            boundary = boundary.Substring(1, boundary.Length - 2);

        return boundary;
    }

    private static async Task<byte[]> ExtractFileFromMultipart(Stream body, string boundary)
    {
        using var ms = new MemoryStream();
        await body.CopyToAsync(ms);
        var allBytes = ms.ToArray();
        var content = System.Text.Encoding.UTF8.GetString(allBytes);

        var boundaryMarker = "--" + boundary;
        var parts = content.Split(new[] { boundaryMarker }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.Trim() == "--" || string.IsNullOrWhiteSpace(part))
                continue;

            // Check if this part contains the "file" field
            if (part.Contains("name=\"file\"", StringComparison.OrdinalIgnoreCase))
            {
                // Find the blank line separating headers from body
                var headerEndIndex = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEndIndex < 0)
                    headerEndIndex = part.IndexOf("\n\n", StringComparison.Ordinal);

                if (headerEndIndex < 0)
                    continue;

                // Calculate byte offset for the body start
                var headerText = part.Substring(0, headerEndIndex);
                var separatorLength = part.Substring(headerEndIndex).StartsWith("\r\n\r\n") ? 4 : 2;

                // Find this part in the original byte array
                var partStartMarker = System.Text.Encoding.UTF8.GetBytes(boundaryMarker);
                var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerText);

                // Simpler approach: find the field header in the byte array
                var fieldHeader = System.Text.Encoding.UTF8.GetBytes("name=\"file\"");
                var headerPos = FindBytes(allBytes, fieldHeader);
                if (headerPos < 0)
                    continue;

                // Find the double CRLF after the header
                var doubleCrLf = System.Text.Encoding.UTF8.GetBytes("\r\n\r\n");
                var bodyStart = FindBytes(allBytes, doubleCrLf, headerPos) + 4;
                if (bodyStart < 4)
                {
                    var doubleNl = System.Text.Encoding.UTF8.GetBytes("\n\n");
                    bodyStart = FindBytes(allBytes, doubleNl, headerPos) + 2;
                    if (bodyStart < 2)
                        continue;
                }

                // Find the next boundary marker
                var nextBoundary = System.Text.Encoding.UTF8.GetBytes("\r\n" + boundaryMarker);
                var bodyEnd = FindBytes(allBytes, nextBoundary, bodyStart);
                if (bodyEnd < 0)
                {
                    nextBoundary = System.Text.Encoding.UTF8.GetBytes("\n" + boundaryMarker);
                    bodyEnd = FindBytes(allBytes, nextBoundary, bodyStart);
                }
                if (bodyEnd < 0)
                    bodyEnd = allBytes.Length;

                var fileData = new byte[bodyEnd - bodyStart];
                Array.Copy(allBytes, bodyStart, fileData, 0, fileData.Length);
                return fileData;
            }
        }

        return null;
    }

    private static int FindBytes(byte[] source, byte[] pattern, int startIndex = 0)
    {
        for (int i = startIndex; i <= source.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return i;
        }
        return -1;
    }

    private static string GetQueryParam(Uri url, string key)
    {
        var query = url.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        query = query.TrimStart('?');
        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private static (int start, int end)? ParsePageRange(string pagesParam)
    {
        if (string.IsNullOrWhiteSpace(pagesParam))
            return null;

        var parts = pagesParam.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
            return (start, end);

        if (parts.Length == 1 && int.TryParse(parts[0], out var single))
            return (single, single);

        return null;
    }

    private static string DetectImageContentType(byte[] imageBytes)
    {
        if (imageBytes.Length >= 8)
        {
            // PNG magic bytes
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return "image/png";

            // JPEG magic bytes
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return "image/jpeg";

            // GIF magic bytes
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                return "image/gif";

            // TIFF magic bytes
            if ((imageBytes[0] == 0x49 && imageBytes[1] == 0x49) || (imageBytes[0] == 0x4D && imageBytes[1] == 0x4D))
                return "image/tiff";
        }

        return "application/octet-stream";
    }
}
