using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessEntity.Services
{
    public sealed class AuthentikSessionManager
    {
        private const string HttpClientName = "AuthentikAuth";
        private const string StatePurpose = "BusinessEntity.Authentik.State";
        private const string AccessTokenName = "access_token";
        private const string RefreshTokenName = "refresh_token";
        private const string IdTokenName = "id_token";
        private const string AccessTokenExpiresAtName = "access_token_expires_at";
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthentikSessionManager> _logger;
        private readonly IDataProtector _stateProtector;
        private readonly string _browserBaseUrl;
        private readonly string _providerSlug;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _scope;
        private readonly TimeSpan _sessionLifetime;
        private readonly TimeSpan _refreshLeadTime;

        public AuthentikSessionManager(
            IHttpContextAccessor httpContextAccessor,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<AuthentikSessionManager> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _stateProtector = dataProtectionProvider.CreateProtector(StatePurpose);

            var section = configuration.GetSection("AuthentikAuth");

            _browserBaseUrl = (
                Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL_FOR_BROWSER")
                ?? section["BaseUrlForBrowser"]
                ?? section["BaseUrl"]
                ?? "http://localhost:9000").TrimEnd('/');

            _providerSlug = section["ProviderSlug"] ?? "be-oidc";
            _clientId = section["ClientId"] ?? throw new InvalidOperationException("AuthentikAuth:ClientId is required.");
            _clientSecret = section["ClientSecret"] ?? throw new InvalidOperationException("AuthentikAuth:ClientSecret is required.");
            _redirectUri = section["RedirectUri"] ?? throw new InvalidOperationException("AuthentikAuth:RedirectUri is required.");
            _scope = section["Scope"] ?? "openid profile email";
            _sessionLifetime = TimeSpan.FromHours(ReadDouble(section["SessionLifetimeHours"], 8));
            _refreshLeadTime = TimeSpan.FromMinutes(ReadDouble(section["RefreshLeadTimeMinutes"], 5));
        }

        public Task<bool> IsUserAuthenticatedAsync()
            => Task.FromResult(_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false);

        public Task<ClaimsPrincipal?> GetCurrentUserAsync()
            => Task.FromResult(_httpContextAccessor.HttpContext?.User);

        public Task<string?> GetUserNameAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return Task.FromResult(
                user?.Identity?.Name
                ?? user?.FindFirst("preferred_username")?.Value
                ?? user?.FindFirst(ClaimTypes.Name)?.Value);
        }

        public Task<string?> GetUserEmailAsync()
        {
            var email = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;
            return Task.FromResult(email);
        }

        public async Task<string?> GetIdentityTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return authResult.Properties?.GetTokenValue(IdTokenName);
        }

        public string GetLoginUrl(string? returnUrl = null)
        {
            var state = _stateProtector.Protect(JsonSerializer.Serialize(new AuthState(NormalizeReturnUrl(returnUrl), DateTimeOffset.UtcNow)));

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri,
                ["response_type"] = "code",
                ["scope"] = _scope,
                ["state"] = state
            };

            return _browserBaseUrl + QueryHelpers.AddQueryString("/application/o/authorize/", query);
        }

        public async Task<string> CompleteLoginAsync(string code, string? state, CancellationToken cancellationToken = default)
        {
            var tokens = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
            if (string.IsNullOrWhiteSpace(tokens.IdToken))
            {
                throw new InvalidOperationException("Authentik did not return an id_token.");
            }

            var principal = CreatePrincipal(tokens.IdToken);
            var properties = CreateAuthenticationProperties(tokens, DateTimeOffset.UtcNow, null, null, null);

            var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HTTP context is not available.");
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            return ReadReturnUrl(state);
        }

        public async Task<string> SignOutAsync(CancellationToken cancellationToken = default)
        {
            var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HTTP context is not available.");
            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var idToken = authResult.Properties?.GetTokenValue(IdTokenName);

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return "/auth/logged-out";
            }

            return BuildFrontChannelLogoutUrl(idToken);
        }

        public async Task RefreshSessionAsync(CookieValidatePrincipalContext context)
        {
            if (context.Principal?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var sessionExpiresUtc = context.Properties.ExpiresUtc;
            if (sessionExpiresUtc == null || sessionExpiresUtc <= now)
            {
                _logger.LogInformation("Authentication cookie expired; signing user out.");
                await RejectSessionAsync(context);
                return;
            }

            var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);
            var currentIdToken = context.Properties.GetTokenValue(IdTokenName);
            var accessTokenExpiresAt = ParseTokenExpiration(context.Properties.GetTokenValue(AccessTokenExpiresAtName));

            if (accessTokenExpiresAt == null)
            {
                _logger.LogWarning("Authentication cookie does not contain access token expiry; signing user out.");
                await RejectSessionAsync(context);
                return;
            }

            if (accessTokenExpiresAt > now.Add(_refreshLeadTime))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogInformation("Refresh token is missing; signing user out.");
                await RejectSessionAsync(context);
                return;
            }

            try
            {
                var refreshedTokens = await RefreshTokensAsync(refreshToken, context.HttpContext.RequestAborted);
                var idToken = string.IsNullOrWhiteSpace(refreshedTokens.IdToken) ? currentIdToken : refreshedTokens.IdToken;
                if (string.IsNullOrWhiteSpace(idToken))
                {
                    throw new InvalidOperationException("No id_token is available after refresh.");
                }

                var nextRefreshToken = string.IsNullOrWhiteSpace(refreshedTokens.RefreshToken) ? refreshToken : refreshedTokens.RefreshToken;
                var principal = CreatePrincipal(idToken);
                var refreshedProperties = CreateAuthenticationProperties(
                    refreshedTokens with { IdToken = idToken, RefreshToken = nextRefreshToken },
                    now,
                    sessionExpiresUtc,
                    nextRefreshToken,
                    idToken);

                context.ReplacePrincipal(principal);
                context.Properties.IsPersistent = refreshedProperties.IsPersistent;
                context.Properties.AllowRefresh = refreshedProperties.AllowRefresh;
                context.Properties.ExpiresUtc = refreshedProperties.ExpiresUtc;
                context.Properties.IssuedUtc = refreshedProperties.IssuedUtc;
                context.Properties.RedirectUri = refreshedProperties.RedirectUri;
                context.Properties.StoreTokens(refreshedProperties.GetTokens());
                context.ShouldRenew = true;

                _logger.LogDebug("Authentication session refreshed for {User}.", principal.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh Authentik session.");
                await RejectSessionAsync(context);
            }
        }

        private async Task<TokenSet> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
        {
            using var request = CreateTokenRequest(
                new Dictionary<string, string?>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = _redirectUri,
                    ["client_id"] = _clientId
                });

            return await SendTokenRequestAsync(request, cancellationToken);
        }

        private async Task<TokenSet> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
        {
            using var request = CreateTokenRequest(
                new Dictionary<string, string?>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = _clientId
                });

            return await SendTokenRequestAsync(request, cancellationToken);
        }

        private async Task<TokenSet> SendTokenRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Authentik token endpoint returned {(int)response.StatusCode}: {responseBody}");
            }

            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            return new TokenSet(
                root.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Token response does not contain access_token."),
                root.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null,
                root.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
                root.GetProperty("expires_in").GetInt32());
        }

        private HttpRequestMessage CreateTokenRequest(IDictionary<string, string?> form)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/application/o/token/");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")));
            request.Content = new FormUrlEncodedContent(form!);
            return request;
        }

        private ClaimsPrincipal CreatePrincipal(string idToken)
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
            var subject = jwt.Subject ?? Guid.NewGuid().ToString("N");
            var userName =
                jwt.Claims.FirstOrDefault(claim => claim.Type == "preferred_username")?.Value
                ?? jwt.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?.Value
                ?? subject;
            var email =
                jwt.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value
                ?? jwt.Claims.FirstOrDefault(claim => claim.Type == "email")?.Value;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, subject),
                new(ClaimTypes.Name, userName)
            };

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            foreach (var claim in jwt.Claims)
            {
                if (claims.Any(existing => existing.Type == claim.Type && existing.Value == claim.Value))
                {
                    continue;
                }

                claims.Add(claim);
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        private AuthenticationProperties CreateAuthenticationProperties(
            TokenSet tokens,
            DateTimeOffset now,
            DateTimeOffset? sessionExpiresUtc,
            string? fallbackRefreshToken,
            string? fallbackIdToken)
        {
            var idToken = string.IsNullOrWhiteSpace(tokens.IdToken) ? fallbackIdToken : tokens.IdToken;
            var refreshToken = string.IsNullOrWhiteSpace(tokens.RefreshToken) ? fallbackRefreshToken : tokens.RefreshToken;

            var storedTokens = new List<AuthenticationToken>
            {
                new() { Name = AccessTokenName, Value = tokens.AccessToken },
                new() { Name = AccessTokenExpiresAtName, Value = now.AddSeconds(tokens.ExpiresIn).ToString("O", CultureInfo.InvariantCulture) }
            };

            if (!string.IsNullOrWhiteSpace(idToken))
            {
                storedTokens.Add(new AuthenticationToken { Name = IdTokenName, Value = idToken });
            }

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                storedTokens.Add(new AuthenticationToken { Name = RefreshTokenName, Value = refreshToken });
            }

            var properties = new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = false,
                ExpiresUtc = sessionExpiresUtc ?? now.Add(_sessionLifetime)
            };

            properties.StoreTokens(storedTokens);
            return properties;
        }

        private string BuildFrontChannelLogoutUrl(string idToken)
        {
            var postLogoutRedirectUri = new Uri(new Uri(_redirectUri), "/auth/logged-out").ToString();

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = _clientId,
                ["post_logout_redirect_uri"] = postLogoutRedirectUri,
                ["id_token_hint"] = idToken
            };

            return _browserBaseUrl + QueryHelpers.AddQueryString($"/application/o/{_providerSlug}/end-session/", query);
        }

        private string ReadReturnUrl(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return "/";
            }

            try
            {
                var payload = JsonSerializer.Deserialize<AuthState>(_stateProtector.Unprotect(state));
                if (payload == null)
                {
                    return "/";
                }

                if (DateTimeOffset.UtcNow - payload.IssuedUtc > StateLifetime)
                {
                    _logger.LogInformation("Protected auth state expired; returning to root.");
                    return "/";
                }

                return NormalizeReturnUrl(payload.ReturnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read auth state; returning to root.");
                return "/";
            }
        }

        private static string NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return "/";
            }

            if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return "/";
            }

            return returnUrl;
        }

        private static DateTimeOffset? ParseTokenExpiration(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
                ? result
                : null;
        }

        private static double ReadDouble(string? value, double defaultValue)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }

        private static async Task RejectSessionAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        private sealed record AuthState(string ReturnUrl, DateTimeOffset IssuedUtc);

        private sealed record TokenSet(string AccessToken, string? IdToken, string? RefreshToken, int ExpiresIn);
    }
}
