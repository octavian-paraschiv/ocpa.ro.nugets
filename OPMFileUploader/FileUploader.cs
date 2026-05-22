using FileUploader.Exceptions;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FileUploader;

public class FileUploader(string requestUrl, string authUrl, string uploadFilePath, string loginId, string password) : BaseUploader(requestUrl, authUrl, loginId, password)
{
    private readonly string _uploadFilePath = uploadFilePath ?? throw new ArgumentNullException(nameof(uploadFilePath));
    public event Action<double> FileUploadProgress;

    protected override async Task<string> PerformUpload(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] compressedData;

        using (var output = new MemoryStream())
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            var inData = await File.ReadAllBytesAsync(_uploadFilePath, cancellationToken);
            await gzip.WriteAsync(inData, cancellationToken);
            gzip.Close();

            compressedData = output.ToArray();
        }

        if (!(compressedData?.Length > 0))
            throw new UploaderException("Failed to compress the data.");

        using (var input = new MemoryStream(compressedData))
        using (var client = new HttpClient())
        using (var multiForm = new MultipartFormDataContent())
        {
            client.Timeout = TimeSpan.FromMinutes(15);
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            client.DefaultRequestHeaders.Authorization = await Authorize(cancellationToken);

            var file = new ProgressableStreamContent
            (
                content: new StreamContent(input),
                cancellationToken: cancellationToken,
                progress: (sent, total) =>
                {
                    total = Math.Max(1, total); // to avoid divide by 0
                    FileUploadProgress?.Invoke(100 * sent / (double)total);
                }
            );

            var fileName = Path.GetFileName(_uploadFilePath);

            var signature = GetHMACSHA1Hash(compressedData, fileName);
            multiForm.Add(new StringContent(signature), "signature"); // Add the file signature
            multiForm.Add(file, "data", fileName); // Add the file

            var response = await client.PostAsync(_requestUrl, multiForm, cancellationToken);

            await AnalyzeResponse(response);
        }

        return string.Empty;
    }

    protected override Task<string> PerformDownload(CancellationToken cancellationToken) => throw new NotImplementedException();

    private sealed class ProgressableStreamContent(HttpContent content, Action<long, long> progress,
        CancellationToken cancellationToken) : HttpContent()
    {
        const int BufferSize = 20 * 1024;

        private readonly HttpContent _content = content ?? throw new ArgumentNullException(nameof(content));
        private readonly Action<long, long> _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        private readonly CancellationToken _cancellationToken = cancellationToken;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return Task.Run(async () =>
            {
                var buffer = new byte[BufferSize];
                TryComputeLength(out long size);
                var uploaded = 0;

                using (var sinput = await _content.ReadAsStreamAsync())
                {
                    while (true)
                    {
                        _cancellationToken.ThrowIfCancellationRequested();

                        var length = sinput.Read(buffer, 0, buffer.Length);
                        if (length <= 0)
                            break;

                        uploaded += length;
                        _progress?.Invoke(uploaded, size);

                        stream.Write(buffer, 0, length);
                        stream.Flush();
                    }
                }

                stream.Flush();
            });
        }

        protected override bool TryComputeLength(out long length)
        {
            length = (_content.Headers?.ContentLength).GetValueOrDefault();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _content.Dispose();

            base.Dispose(disposing);
        }
    }
}
