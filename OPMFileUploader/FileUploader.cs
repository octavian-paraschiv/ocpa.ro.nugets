using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OPMFileUploader
{
    public class FileUploader
    {
        private readonly string _uploadUrl;
        private readonly string _authUrl;
        private readonly string _uploadFilePath;

        private readonly string _loginId;
        private readonly string _password;

        private readonly ManualResetEventSlim _completed = new ManualResetEventSlim(false);

        public event Action<double> FileUploadProgress;

        private AuthenticateResponse _tokenResponse = null;


        public FileUploader(string uploadUrl, string authUrl, string uploadFilePath, string loginId, string password)
        {
            _uploadUrl = uploadUrl ?? throw new ArgumentNullException(nameof(uploadUrl));
            _authUrl = authUrl ?? throw new ArgumentNullException(nameof(authUrl));
            _uploadFilePath = uploadFilePath ?? throw new ArgumentNullException(nameof(uploadFilePath));

            _loginId = loginId;
            _password = password;
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

                    var response = await client.PostAsync(_uploadUrl, multiForm);
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

        private static string GetHMACSHA1Hash(byte[] inputBytes, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            using (var ms = new MemoryStream(inputBytes))
            using (var hmac = new HMACSHA1(keyBytes))
            {
                var hash = hmac.ComputeHash(ms);
                return Convert.ToBase64String(hash);
            }
        }

        private async Task<AuthenticationHeaderValue> Authorize(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_loginId))
                return null;

            if (_tokenResponse == null ||
                DateTime.UtcNow.Subtract(_tokenResponse.Expires).TotalMilliseconds > 0)
            {
                var dict = new Dictionary<string, string>
                {
                    { "LoginId", _loginId },
                    { "Password", Authentication.sendHash(_loginId, _password) }
                };

                var content = new FormUrlEncodedContent(dict);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                try
                {
                    using (HttpClient cl = new HttpClient())
                    {
                        cl.Timeout = TimeSpan.FromSeconds(30);
                        cl.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                        var x = await cl.PostAsync(_authUrl, content, cancellationToken);

                        string rspBody = (x?.Content != null) ? await x.Content.ReadAsStringAsync() : null;

                        if (!string.IsNullOrEmpty(rspBody))
                            _tokenResponse = JsonSerializer.Deserialize<AuthenticateResponse>(rspBody);
                    }
                }
                catch
                {
                    _tokenResponse = null;
                }
            }

            if (_tokenResponse?.Token == null)
                throw new HttpRequestException("Unauthorized");

            return new AuthenticationHeaderValue("Bearer", _tokenResponse.Token);
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

        private sealed class AuthenticateResponse
        {
            public string LoginId { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public DateTime Expires { get; set; } = DateTime.MinValue;
        }
    }
}
