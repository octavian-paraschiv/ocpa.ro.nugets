using FileUploader;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OPMFileUploader
{
    public class FileUploader : BaseUploader
    {
        private readonly string _uploadFilePath;
        public event Action<double> FileUploadProgress;

        public FileUploader(string requestUrl, string authUrl, string uploadFilePath, string loginId, string password)
            : base(requestUrl, authUrl, loginId, password)
        {
            _uploadFilePath = uploadFilePath ?? throw new ArgumentNullException(nameof(uploadFilePath));
        }

        public async Task<string> Run()
        {
            if (_completed.Wait(0))
                throw new InvalidOperationException($"A {nameof(FileUploader)} instance cannot be reused. Create a new instance to upload another file.");

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

        private async Task<string> PerformUpload(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] compressedData;

                using (MemoryStream output = new MemoryStream())
                using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    var inData = File.ReadAllBytes(_uploadFilePath);
                    await gzip.WriteAsync(inData, 0, inData.Length, cancellationToken);
                    gzip.Close();

                    compressedData = output.ToArray();
                }

                if (!(compressedData?.Length > 0))
                    throw new OperationCanceledException();

                using (MemoryStream input = new MemoryStream(compressedData))
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
                            FileUploadProgress?.Invoke((100 * sent) / (double)total);
                        }
                    );

                    var fileName = Path.GetFileName(_uploadFilePath);

                    var signature = GetHMACSHA1Hash(compressedData, fileName);
                    multiForm.Add(new StringContent(signature), "signature"); // Add the file signature
                    multiForm.Add(file, "data", fileName); // Add the file

                    var response = await client.PostAsync(_requestUrl, multiForm);
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
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }

        private sealed class ProgressableStreamContent : HttpContent
        {
            const int BufferSize = 20 * 1024;

            private readonly HttpContent _content;
            private readonly Action<long, long> _progress;
            private readonly CancellationToken _cancellationToken;

            public ProgressableStreamContent(HttpContent content, Action<long, long> progress,
                CancellationToken cancellationToken) : base()
            {
                _content = content ?? throw new ArgumentNullException(nameof(content));
                _progress = progress ?? throw new ArgumentNullException(nameof(progress));
                _cancellationToken = cancellationToken;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return Task.Run(async () =>
                {
                    var buffer = new byte[BufferSize];
                    long size;
                    TryComputeLength(out size);
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
}
