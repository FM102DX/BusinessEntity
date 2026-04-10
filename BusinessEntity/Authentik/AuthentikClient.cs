using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace BusinessEntity.Authentik
{
    /// <summary>
    /// Thin REST client for Authentik API v3. Encapsulates endpoints used by bootstrap
    /// to ensure OAuth2/OIDC provider and application existence. Includes simple retry
    /// for transient gateway errors (502/503/504).
    /// </summary>
    internal class AuthentikClient
    {
        private readonly HttpClient _http;

        /// <summary>
        /// Creates a client with a provided HttpClient. Timeout defaults to 30s if unset.
        /// </summary>
        public AuthentikClient(HttpClient http)
        {
            _http = http;
            if (_http.Timeout == default)
                _http.Timeout = TimeSpan.FromSeconds(30);
            // BaseAddress can be supplied via ENV at registration time; if not, methods will combine with provided baseUrl
        }

        private static Uri Combine(Uri baseUri, string relative) => new Uri(baseUri, relative);

        private static HttpRequestMessage CreateJsonRequest(HttpMethod method, Uri uri, string? bearerToken, object? body = null)
        {
            var req = new HttpRequestMessage(method, uri);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(bearerToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (body != null)
            {
                req.Content = JsonContent.Create(body);
            }
            return req;
        }

        /// <summary>
        /// Sends an HTTP request and retries on 502/503/504 with exponential backoff.
        /// Returns the final HttpResponseMessage (caller disposes).
        /// </summary>
        private async Task<HttpResponseMessage> SendAsyncWithRetry(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
        {
            const int maxAttempts = 5;
            var delay = TimeSpan.FromMilliseconds(200);
            HttpResponseMessage? last = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                last?.Dispose();
                using var req = requestFactory();
                var resp = await _http.SendAsync(req, ct);
                if ((int)resp.StatusCode != 502 && (int)resp.StatusCode != 503 && (int)resp.StatusCode != 504)
                {
                    return resp; // caller disposes
                }
                last = resp;
                if (attempt < maxAttempts)
                    await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            return last!;
        }

        /// <summary>
        /// Reads and deserializes JSON body to <typeparamref name="T"/> or throws with
        /// detailed error (status, reason, body) when response is not successful or empty.
        /// </summary>
        private static async Task<T> ReadJsonOrThrow<T>(HttpResponseMessage resp, string errorContext)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Authentik API error ({errorContext}): {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {content}");
            }

            var stream = await resp.Content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (data == null)
                throw new InvalidOperationException($"Authentik API error ({errorContext}): empty response body");
            return data;
        }

        /// <summary>
        /// Checks Authentik API availability via /api/v3/root/config/ and returns a version object.
        /// </summary>
        public async Task<VersionDto> GetVersion(Uri baseUrl, string token, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, "/api/v3/root/config/");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Get, uri, token), ct);
            // Just check that API is accessible, version info not available in this endpoint
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Authentik API error (root/config): {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {content}");
            }
            return new VersionDto { Version = "2026.x" }; // Version check successful, API is accessible
        }

        /// <summary>
        /// Returns a flow for the given designation, preferring known default slugs first.
        /// </summary>
        private async Task<FlowDto?> FindFlow(Uri baseUrl, string token, string designation, string[] preferredSlugs, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, $"/api/v3/flows/instances/?designation={Uri.EscapeDataString(designation)}");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Get, uri, token), ct);
            var page = await ReadJsonOrThrow<PagedResult<FlowDto>>(resp, $"flows/instances?designation={designation}");

            foreach (var slug in preferredSlugs)
            {
                var match = page.Results.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return page.Results.FirstOrDefault();
        }

        /// <summary>
        /// Tries the known default authorization flows and returns the best match.
        /// </summary>
        public Task<FlowDto?> FindAuthorizationFlow(Uri baseUrl, string token, CancellationToken ct = default)
        {
            return FindFlow(
                baseUrl,
                token,
                "authorization",
                new[] { "default-provider-authorization-explicit-consent", "default-provider-authorization-implicit-consent" },
                ct);
        }

        /// <summary>
        /// Tries the known default invalidation/logout flows and returns the best match.
        /// </summary>
        public Task<FlowDto?> FindInvalidationFlow(Uri baseUrl, string token, CancellationToken ct = default)
        {
            return FindFlow(
                baseUrl,
                token,
                "invalidation",
                new[] { "default-provider-invalidation-flow", "default-invalidation-flow" },
                ct);
        }

        /// <summary>
        /// Fetches provider by client_id (paged query then in-memory match).
        /// </summary>
        public async Task<ProviderDto?> GetProviderByClientId(Uri baseUrl, string token, string clientId, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, $"/api/v3/providers/oauth2/?client_id={Uri.EscapeDataString(clientId)}");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Get, uri, token), ct);
            var page = await ReadJsonOrThrow<PagedResult<ProviderDto>>(resp, "providers/oauth2?client_id");
            return page.Results.FirstOrDefault(p => string.Equals(p.ClientId, clientId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Creates an OAuth2/OIDC provider.
        /// </summary>
        public async Task<ProviderDto> CreateProvider(Uri baseUrl, string token, ProviderCreateDto dto, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, "/api/v3/providers/oauth2/");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Post, uri, token, dto), ct);
            return await ReadJsonOrThrow<ProviderDto>(resp, "providers/oauth2 POST");
        }

        /// <summary>
        /// Patches provider fields (redirect URIs and/or client secret).
        /// </summary>
        public async Task<ProviderDto> PatchProvider(Uri baseUrl, string token, int pk, ProviderPatchDto dto, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, $"/api/v3/providers/oauth2/{pk}/");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Patch, uri, token, dto), ct);
            return await ReadJsonOrThrow<ProviderDto>(resp, "providers/oauth2 PATCH");
        }

        /// <summary>
        /// Fetches application by slug.
        /// </summary>
        public async Task<ApplicationDto?> GetApplicationBySlug(Uri baseUrl, string token, string slug, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, $"/api/v3/core/applications/?slug={Uri.EscapeDataString(slug)}");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Get, uri, token), ct);
            var page = await ReadJsonOrThrow<PagedResult<ApplicationDto>>(resp, "core/applications?slug");
            return page.Results.FirstOrDefault(a => string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates an Authentik application.
        /// </summary>
        public async Task<ApplicationDto> CreateApplication(Uri baseUrl, string token, ApplicationCreateDto dto, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, "/api/v3/core/applications/");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Post, uri, token, dto), ct);
            return await ReadJsonOrThrow<ApplicationDto>(resp, "core/applications POST");
        }

        /// <summary>
        /// Patches application to associate with a provider.
        /// </summary>
        public async Task<ApplicationDto> PatchApplication(Uri baseUrl, string token, string pk, ApplicationPatchDto dto, CancellationToken ct = default)
        {
            var uri = Combine(baseUrl, $"/api/v3/core/applications/{pk}/");
            using var resp = await SendAsyncWithRetry(() => CreateJsonRequest(HttpMethod.Patch, uri, token, dto), ct);
            return await ReadJsonOrThrow<ApplicationDto>(resp, "core/applications PATCH");
        }
    }
}
