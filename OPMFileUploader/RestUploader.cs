using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileUploader;

public class RestUploader<T> : BaseUploader where T : class
{
    private readonly T _uploadData;
    private readonly bool _useCompression;

    public RestUploader(string requestUrl, string authUrl, T uploadData, string loginId, string password, bool useCompression)
           : base(requestUrl, authUrl, loginId, password)
    {
        _uploadData = uploadData ?? throw new ArgumentNullException(nameof(uploadData));
        _useCompression = useCompression;
    }

    public async Task<string> Run()
    {
        if (_completed.Wait(0))
            throw new InvalidOperationException($"A {nameof(RestUploader<T>)} instance cannot be reused. Create a new instance to upload other data.");

        string s = null;

        try
        {
            s = await PerformUpload(new CancellationTokenSource().Token);
        }
        catch (Exception ex)
        {
            s = ex.Message;
        }
        finally
        {
            _completed.Set();
        }

        return s;
    }

    public async Task<string> PerformGet(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var client = new HttpClient();
        using var ms = new MemoryStream();

        client.Timeout = TimeSpan.FromMinutes(15);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        client.DefaultRequestHeaders.Authorization = await Authorize(cancellationToken);

        var response = await client.GetAsync(_requestUrl, cancellationToken);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            if (response.Content != null)
            {
                string res = "";

                try
                {
                    res = await response.Content.ReadAsStringAsync();
                    return res;
                }
                catch
                {
                }

                throw new OperationCanceledException($"{response.StatusCode}: {res}");
            }

            throw new OperationCanceledException($"{response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PerformUpload(CancellationToken cancellationToken)
    {
        try
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
                brotli.Write(jsonBytes, 0, jsonBytes.Length);
            }
            else
            {
                ms.Write(jsonBytes);
            }

            ms.Position = 0;

            response = await client.PostAsync(_requestUrl, content);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.Content != null)
                {
                    string res = "";

                    try
                    {
                        res = await response.Content.ReadAsStringAsync();
                    }
                    catch
                    {
                        res = "";
                    }

                    throw new OperationCanceledException($"{response.StatusCode}: {res}");
                }

                throw new OperationCanceledException($"{response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return string.Empty;
    }
}
