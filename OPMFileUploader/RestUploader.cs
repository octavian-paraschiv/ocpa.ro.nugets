using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileUploader;

public class RestUploader<T>(string requestUrl, string authUrl, T uploadData, string loginId, string password, bool useCompression)
    : BaseUploader(requestUrl, authUrl, loginId, password) where T : class
{
    private readonly T _uploadData = uploadData ?? throw new ArgumentNullException(nameof(uploadData));
    private readonly bool _useCompression = useCompression;

    protected override async Task<string> PerformDownload(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var client = new HttpClient();
        using var ms = new MemoryStream();

        client.Timeout = TimeSpan.FromMinutes(15);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        client.DefaultRequestHeaders.Authorization = await Authorize(cancellationToken);

        var response = await client.GetAsync(_requestUrl, cancellationToken);

        await AnalyzeResponse(response);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    protected override async Task<string> PerformUpload(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var client = new HttpClient();
        using var ms = new MemoryStream();

        client.Timeout = TimeSpan.FromMinutes(15);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        client.DefaultRequestHeaders.Authorization = await Authorize(cancellationToken);

        var json = JsonSerializer.Serialize(_uploadData);
        var jsonBytes = Encoding.UTF8.GetBytes(json);


        var content = new StreamContent(ms);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;

        if (_useCompression)
        {
            using var brotli = new BrotliStream(ms, CompressionMode.Compress, true);
            await brotli.WriteAsync(jsonBytes, cancellationToken);
        }
        else
        {
            await ms.WriteAsync(jsonBytes, cancellationToken);
        }

        ms.Position = 0;

        response = await client.PostAsync(_requestUrl, content, cancellationToken);

        await AnalyzeResponse(response);

        return string.Empty;
    }
}
