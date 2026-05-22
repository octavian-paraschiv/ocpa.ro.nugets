using FileUploader.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FileUploader;

public abstract class BaseUploader(string requestUrl, string authUrl, string loginId, string password)
{
    protected readonly string _requestUrl = requestUrl ?? throw new ArgumentNullException(nameof(requestUrl));
    protected readonly string _authUrl = authUrl ?? throw new ArgumentNullException(nameof(authUrl));

    protected readonly string _loginId = loginId;
    protected readonly string _password = password;

    protected static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions
    {
        ExpirationScanFrequency = TimeSpan.FromSeconds(10),
    });

    public async Task<string> Upload(CancellationToken cancellationToken)
    {
        try
        {
            return await PerformUpload(cancellationToken);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string> Download(CancellationToken cancellationToken)
    {
        try
        {
            return await PerformDownload(cancellationToken);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    protected abstract Task<string> PerformUpload(CancellationToken cancellationToken);

    protected abstract Task<string> PerformDownload(CancellationToken cancellationToken);


    protected static string GetHMACSHA1Hash(byte[] inputBytes, string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);

        using var ms = new MemoryStream(inputBytes);
        using var hmac = new HMACSHA1(keyBytes);

        var hash = hmac.ComputeHash(ms);

        return Convert.ToBase64String(hash);
    }

    protected async Task<AuthenticationHeaderValue> Authorize(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(_loginId))
            return null;

        var key = $"{_loginId}@{_authUrl}";

        if (_memoryCache.TryGetValue(key, out string tok))
            return new AuthenticationHeaderValue("Bearer", tok);

        var dict = new Dictionary<string, string>
            {
                { "LoginId", _loginId },
                { "Password", Authentication.SendHash(_loginId, _password) }
            };

        var content = new FormUrlEncodedContent(dict);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        AuthenticateResponse authenticateResponse = null;

        try
        {
            using var cl = new HttpClient();

            cl.Timeout = TimeSpan.FromSeconds(30);
            cl.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            var rsp = await cl.PostAsync(_authUrl, content, cancellationToken);

            await AnalyzeResponse(rsp);

            string rspBody = (rsp?.Content != null) ? await rsp.Content.ReadAsStringAsync(cancellationToken) : null;

            if (!string.IsNullOrEmpty(rspBody))
                authenticateResponse = JsonSerializer.Deserialize<AuthenticateResponse>(rspBody);
        }
        catch
        {
            authenticateResponse = null;
        }

        if (authenticateResponse?.Token == null)
            throw new HttpRequestException("Unauthorized");

        if (authenticateResponse.Validity > 0)
        {
            _memoryCache.Set(key, authenticateResponse.Token, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(authenticateResponse.Validity),
            });
        }

        return new AuthenticationHeaderValue("Bearer", authenticateResponse.Token);
    }

    protected static async Task AnalyzeResponse(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            if (response.Content != null)
            {
                string res;

                try
                {
                    res = await response.Content.ReadAsStringAsync();
                }
                catch
                {
                    res = "";
                }

                throw new UploaderException($"{response.StatusCode}: {res}");
            }

            throw new UploaderException($"{response.StatusCode}");
        }
    }

    protected sealed class AuthenticateResponse
    {
        [JsonPropertyName("loginId")]
        public string LoginId { get; set; } = string.Empty;

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("validity")]
        public int Validity { get; set; }
    }
}
