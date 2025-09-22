using Microsoft.Extensions.Caching.Memory;
using OPMFileUploader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FileUploader;

public abstract class BaseUploader
{
    protected readonly string _requestUrl;
    protected readonly string _authUrl;

    protected readonly string _loginId;
    protected readonly string _password;

    protected readonly ManualResetEventSlim _completed = new ManualResetEventSlim(false);

    protected static MemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions
    {
        ExpirationScanFrequency = TimeSpan.FromSeconds(10),
    });

    public BaseUploader(string requestUrl, string authUrl, string loginId, string password)
    {
        _requestUrl = requestUrl ?? throw new ArgumentNullException(nameof(requestUrl));
        _authUrl = authUrl ?? throw new ArgumentNullException(nameof(authUrl));

        _loginId = loginId;
        _password = password;
    }

    protected static string GetHMACSHA1Hash(byte[] inputBytes, string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);

        using (var ms = new MemoryStream(inputBytes))
        using (var hmac = new HMACSHA1(keyBytes))
        {
            var hash = hmac.ComputeHash(ms);
            return Convert.ToBase64String(hash);
        }
    }

    protected async Task<AuthenticationHeaderValue> Authorize(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_loginId))
            return null;

        var key = $"{_loginId}@{_authUrl}";

        if (_memoryCache.TryGetValue(key, out string tok))
            return new AuthenticationHeaderValue("Bearer", tok);

        var dict = new Dictionary<string, string>
            {
                { "LoginId", _loginId },
                { "Password", Authentication.sendHash(_loginId, _password) }
            };

        var content = new FormUrlEncodedContent(dict);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        AuthenticateResponse authenticateResponse = null;

        try
        {
            using HttpClient cl = new HttpClient();
            cl.Timeout = TimeSpan.FromSeconds(30);
            cl.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            var x = await cl.PostAsync(_authUrl, content, cancellationToken);

            string rspBody = (x?.Content != null) ? await x.Content.ReadAsStringAsync() : null;

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
