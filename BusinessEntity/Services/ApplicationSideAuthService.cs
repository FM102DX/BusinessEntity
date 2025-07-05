using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessEntity.Services
{
    public class ApplicationSideAuthService : IApplicationSideAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ApplicationSideAuthService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private string? _providerSlug;
        private string? _lastEndSessionUrl; // Сохраняем URL для фронт-ченнел logout

        public ApplicationSideAuthService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<ApplicationSideAuthService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        #region Получение параметров пользователя и токенов

        public Task<bool> IsUserAuthenticatedAsync()
            => Task.FromResult(_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false);

        public Task<ClaimsPrincipal?> GetCurrentUserAsync()
            => Task.FromResult(_httpContextAccessor.HttpContext?.User);

        public Task<string?> GetUserNameAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userName = user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value;
            return Task.FromResult(userName);
        }

        public Task<string?> GetUserEmailAsync()
        {
            var email = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
            return Task.FromResult(email);
        }

        public Task<string?> GetJwtTokenAsync()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return Task.FromResult<string?>(null);

            if (ctx.Request.Cookies.TryGetValue("jwt_token", out var jwt) && !string.IsNullOrEmpty(jwt))
                return Task.FromResult<string?>(jwt);

            var auth = ctx.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer "))
                return Task.FromResult<string?>(auth.Substring("Bearer ".Length).Trim());

            return Task.FromResult<string?>(null);
        }

        #endregion

        #region Проверка и валидация токенов

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var cfg = _configuration.GetSection("AuthentIC2");
                var client = _httpClientFactory.CreateClient("AuthentIC2");
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg["ClientId"]}:{cfg["ClientSecret"]}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);

                var content = new FormUrlEncodedContent(new Dictionary<string, string?>
                {
                    ["token"] = token
                });

                _logger.LogInformation("[ValidateToken] introspecting token");
                var resp = await client.PostAsync("/application/o/introspect/", content);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[ValidateToken] HTTP {Status}", resp.StatusCode);
                    return false;
                }

                var body = await resp.Content.ReadAsStringAsync();
                var j = JsonSerializer.Deserialize<JsonElement>(body);
                return j.GetProperty("active").GetBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ValidateToken] error");
                return false;
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AuthentIC2");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var resp = await client.GetAsync("/-/health/live/", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[IsServiceAvailableAsync] health check failed");
                return false;
            }
        }

        #endregion

        #region OAuth процессы и обмен кодов

        public async Task<TokenResponseAuthenticCustom> ExchangeCodeAsync(string code)
        {
            var cfg = _configuration.GetSection("AuthentIC2");
            var client = _httpClientFactory.CreateClient("AuthentIC2");

            var form = new Dictionary<string, string?>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = cfg["RedirectUri"],
                ["client_id"] = cfg["ClientId"]
            };
            var content = new FormUrlEncodedContent(form);

            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg["ClientId"]}:{cfg["ClientSecret"]}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);

            var resp = await client.PostAsync("/application/o/token/", content);
            var body = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            var doc = JsonDocument.Parse(body);
            var at = doc.RootElement.GetProperty("access_token").GetString()!;
            var idt = doc.RootElement.GetProperty("id_token").GetString()!;
            string? rft = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;

            return new TokenResponseAuthenticCustom(at, idt, rft);
        }

        public string GetLoginUrl(string? returnUrl = null)
        {
            var cfg = _configuration.GetSection("AuthentIC2");
            var baseUrlForBrowser = cfg["BaseUrlForBrowser"]!.TrimEnd('/'); ;
            var clientId = cfg["ClientId"]!;
            var redirect = cfg["RedirectUri"]!;
            var scope = cfg["Scope"] ?? "openid profile email";
            var state = string.IsNullOrEmpty(returnUrl)
                ? Guid.NewGuid().ToString("N")
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(returnUrl));

            var qs = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirect,
                ["response_type"] = "code",
                ["scope"] = scope,
                ["state"] = state,
                ["prompt"] = "login", // Принудительно требуем ввод логина каждый раз
                ["max_age"] = "0",
                ["include_granted_scopes"] = "false"
            };
            return QueryHelpers.AddQueryString($"{baseUrlForBrowser}/application/o/authorize/", qs);
        }

        public async Task<OAuthCallbackResult> ProcessOAuthCallbackAsync(string code)
        {
            try
            {
                // 1) exchange
                var tokens = await ExchangeCodeAsync(code);
                if (string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.IdToken))
                    return OAuthCallbackResult.Failure("Failed to obtain tokens");

                // 2) introspect
                if (!await ValidateTokenAsync(tokens.AccessToken))
                    return OAuthCallbackResult.Failure("Invalid access token");

                // 3) build principal
                var principal = await CreateUserPrincipalAsync(tokens);
                var name = principal.Identity?.Name ?? "Unknown";
                return OAuthCallbackResult.Success(principal, tokens, name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessOAuthCallbackAsync] error");
                return OAuthCallbackResult.Failure("Authentication failed");
            }
        }

        public Task<ClaimsPrincipal> CreateUserPrincipalAsync(TokenResponseAuthenticCustom tokens)
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokens.IdToken);

            var userId = jwt.Subject ?? Guid.NewGuid().ToString();
            var userName = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username"
                                                         || c.Type == ClaimTypes.Name)?.Value
                           ?? userId;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name,           userName),
                new Claim("jwt_token",               tokens.IdToken)
            };
            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(ClaimTypes.Email, email));

            foreach (var cl in jwt.Claims)
                if (!claims.Any(c => c.Type == cl.Type && c.Value == cl.Value))
                    claims.Add(cl);

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return Task.FromResult(new ClaimsPrincipal(identity));
        }

        #endregion

        #region Выход из системы и логаут

        public async Task<bool> SignOutAsync()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.User?.Identity?.IsAuthenticated != true) 
            {
                _logger.LogInformation("[SignOutAsync] No authenticated user to sign out");
                return true; // Пользователь уже не аутентифицирован - считаем успехом
            }

            var idToken = ctx.Request.Cookies["jwt_token"];
            if (!string.IsNullOrEmpty(idToken))
            {
                try
                {
                    var cfg = _configuration.GetSection("AuthentIC2");
                    // Сохраняем URL для фронт-ченнел logout
                    _lastEndSessionUrl = BuildFrontChannelEndSessionUrl(idToken, cfg);
                    
                    var ok = await LogoutFromAuthentikAsync(idToken);
                    if (!ok) 
                    {
                        _logger.LogError("[SignOutAsync] Failed to logout from Authentik");
                        throw new AuthSignOutFromAuthenticException("Failed to logout from Authentik authentication server");
                    }
                    _logger.LogInformation("[SignOutAsync] Successfully logged out from Authentik (back-channel)");
                }
                catch (AuthSignOutFromAuthenticException)
                {
                    // Пропускаем наше исключение дальше
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SignOutAsync] Unexpected error during Authentik logout");
                    throw new AuthSignOutFromAuthenticException("Unexpected error during Authentik logout", ex);
                }
            }

            try
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ctx.Response.Cookies.Delete("jwt_token");
                ctx.Response.Cookies.Delete("access_token");
                _logger.LogInformation("[SignOutAsync] Local sign out completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignOutAsync] Failed to complete local sign out");
                throw new AuthSignOutFromAuthenticException("Failed to complete local sign out", ex);
            }
        }

        public string? GetFrontChannelLogoutUrl() => _lastEndSessionUrl;

        private async Task<bool> LogoutFromAuthentikAsync(string idToken)
        {
            var cfg = _configuration.GetSection("AuthentIC2");
            var client = _httpClientFactory.CreateClient("AuthentIC2");

            // health-check
            try { await client.GetAsync("/-/health/live/"); }
            catch { /* ignore */ }

            var revokeOk = await RevokeAsync(idToken, client, cfg);
            var endOk = await EndSessionAsync(idToken, client, cfg);
            _logger.LogInformation("[Logout] revoke={Revoke} end-session={End}", revokeOk, endOk);
            return revokeOk || endOk;
        }

        private async Task<bool> RevokeAsync(string token, HttpClient client, IConfigurationSection cfg)
        {
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg["ClientId"]}:{cfg["ClientSecret"]}"));
            var req = new HttpRequestMessage(HttpMethod.Post, "/application/o/revoke/");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
                ["token_type_hint"] = "access_token"
            });
            var resp = await client.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        private async Task<bool> EndSessionAsync(string idToken, HttpClient client, IConfigurationSection cfg)
        {
            var url = BuildEndSessionUrl(idToken, cfg);
            _logger.LogInformation("[EndSessionAsync] GET {Url}", url);
            var resp = await client.GetAsync(url);
            return resp.IsSuccessStatusCode
                   || resp.StatusCode == System.Net.HttpStatusCode.Redirect;
        }

        #endregion

        #region Вспомогательные методы

        public string GetSafeReturnUrl(string? state)
        {
            const string def = "/";
            if (string.IsNullOrEmpty(state)) return def;

            try
            {
                var dec = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                return dec.StartsWith("/") ? dec : def;
            }
            catch
            {
                return state.StartsWith("/") ? state : def;
            }
        }

        private string BuildEndSessionUrl(string idToken, IConfigurationSection cfg)
        {
            var baseUrl = cfg["BaseUrl"]!.TrimEnd('/');
            var slug = _providerSlug ??= ResolveProviderSlug(idToken, cfg);
            var path = $"/application/o/{slug}/end-session/";
            var qs = new Dictionary<string, string?>
            {
                ["client_id"] = cfg["ClientId"],
                ["post_logout_redirect_uri"] = cfg["RedirectUri"],
                ["id_token_hint"] = idToken
            };
            return baseUrl + QueryHelpers.AddQueryString(path, qs);
        }

        private string ResolveProviderSlug(string jwt, IConfigurationSection cfg)
        {
            var fromCfg = cfg["ProviderSlug"];
            if (!string.IsNullOrEmpty(fromCfg))
                return fromCfg.Trim();

            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(jwt);
                var iss = token.Issuer.TrimEnd('/');
                return iss.Split('/').Last();
            }
            catch
            {
                _logger.LogWarning("[ResolveProviderSlug] fallback to 'default'");
                return "default";
            }
        }

        private string BuildFrontChannelEndSessionUrl(string idToken, IConfigurationSection cfg)
        {
            var baseUrlForBrowser = cfg["BaseUrlForBrowser"]!.TrimEnd('/');
            var slug = _providerSlug ??= ResolveProviderSlug(idToken, cfg);
            var path = $"/application/o/{slug}/end-session/";
            var qs = new Dictionary<string, string?>
            {
                ["client_id"] = cfg["ClientId"],
                ["post_logout_redirect_uri"] = "/auth/logged-out", // Перенаправляем на нашу страницу
                ["id_token_hint"] = idToken
            };
            return baseUrlForBrowser + QueryHelpers.AddQueryString(path, qs);
        }

        #endregion
    }
}
