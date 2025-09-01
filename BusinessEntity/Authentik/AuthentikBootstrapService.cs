using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Authentik
{
    /// <summary>
    /// Сервис инициализации (bootstrap) Authentik: гарантирует наличие OIDC-провайдера
    /// и приложения с корректными Redirect URI, client_id/secret. Возвращает итоговые
    /// настройки для конфигурации OpenID Connect в .NET приложении.
    /// </summary>
    internal static class AuthentikBootstrapService
    {
        /// <summary>
        /// Идемпотентно гарантирует провайдера и приложение в Authentik.
        /// Читает ENV/конфиг, при необходимости создаёт/патчит ресурсы и
        /// формирует <see cref="CreatedOidcSettings"/> для регистрации OIDC.
        /// </summary>
        public static CreatedOidcSettings Ensure(string appName, IConfiguration configuration, ILogger logger, IWebLoggerService? webLogger = null)
        {
            // Read flags
            var ensureFlag = (Environment.GetEnvironmentVariable("EnsureAuthentikOnStartup") ?? "true").Trim();
            var ensure = !string.Equals(ensureFlag, "false", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(ensureFlag, "0", StringComparison.OrdinalIgnoreCase);

            // Base URLs
            var baseUrlStr = Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL")
                              ?? configuration["AuthentIC2:BaseUrl"]
                              ?? string.Empty;
            var browserBaseUrlStr = Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL_FOR_BROWSER")
                                   ?? configuration["AuthentIC2:BaseUrlForBrowser"]
                                   ?? baseUrlStr;

            var token = Environment.GetEnvironmentVariable("AUTHENTIK_API_TOKEN")
                        ?? configuration["AuthentIC2:ApiToken"]
                        ?? string.Empty;

            var slug = Environment.GetEnvironmentVariable("AUTHENTIK_SLUG")
                       ?? configuration["AuthentIC2:ProviderSlug"]
                       ?? Slugifier.ToSlug(appName);

            var redirectUris = ParseRedirectUris(
                Environment.GetEnvironmentVariable("AUTHENTIK_REDIRECT_URIS")
                ?? configuration["AuthentIC2:RedirectUri"]
                ?? "http://localhost:7000/signin-oidc");
            // Always include '/signin-oidc' variant for built-in OpenID Connect handler
            redirectUris = AugmentWithSigninOidc(redirectUris);

            // Prefer provided client id/secret
            var clientId = Environment.GetEnvironmentVariable("AUTHENTIK_CLIENT_ID")
                           ?? configuration["AuthentIC2:ClientId"]
                           ?? slug;
            var clientSecret = Environment.GetEnvironmentVariable("AUTHENTIK_CLIENT_SECRET")
                               ?? configuration["AuthentIC2:ClientSecret"]
                               ?? GenerateToken(96);

            var result = new CreatedOidcSettings
            {
                Authority = browserBaseUrlStr.TrimEnd('/') + $"/application/o/{slug}/",
                ClientId = clientId,
                ClientSecret = clientSecret,
                RedirectUris = redirectUris,
                Slug = slug
            };

            var tokenMasked = Mask(token);
            var secretMasked = string.IsNullOrWhiteSpace(clientSecret) ? "<empty>" : Mask(clientSecret);
            _ = webLogger?.Information($"[AK] Bootstrap start: ensure={ensure}, slug={slug}, authority={result.Authority}, clientId={clientId}, redirects=[{string.Join(",", redirectUris)}], baseUrl={baseUrlStr}, browserBaseUrl={browserBaseUrlStr}, token={tokenMasked}");
            logger.LogInformation("[AK] Bootstrap start: ensure={Ensure}, slug={Slug}, authority={Authority}, clientId={ClientId}, redirects={Redirects}, baseUrl={BaseUrl}, browserBaseUrl={BrowserBaseUrl}, apiToken={TokenMasked}, clientSecret={SecretMasked}",
                ensure, slug, result.Authority, clientId, string.Join(";", redirectUris), baseUrlStr, browserBaseUrlStr, tokenMasked, secretMasked);

            if (!ensure)
            {
                logger.LogInformation("EnsureAuthentikOnStartup=false; skipping bootstrap. Using configuration values for OIDC.");
                _ = webLogger?.Information("[AK] Skipping bootstrap: EnsureAuthentikOnStartup=false");
                return result;
            }

            if (string.IsNullOrWhiteSpace(baseUrlStr) || string.IsNullOrWhiteSpace(token))
            {
                _ = webLogger?.Error("[AK] Missing required ENV: AUTHENTIK_BASE_URL or AUTHENTIK_API_TOKEN");
                throw new InvalidOperationException(
                    "Authentik bootstrap requires AUTHENTIK_BASE_URL and AUTHENTIK_API_TOKEN. Set them or disable EnsureAuthentikOnStartup=false.");
            }

            try
            {
                var baseUrl = new Uri(baseUrlStr.TrimEnd('/') + "/");
                _ = webLogger?.Information($"[AK] Contacting Authentik at {baseUrl}");
                logger.LogInformation("[AK] Resolved base URL: {BaseUrl}", baseUrl);
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var client = new AuthentikClient(http);

                // Version check with retry/backoff to handle Authentik warm-up (HTML 404 or gateway errors)
                VersionDto? version = null;
                var sw = Stopwatch.StartNew();
                var attempt = 0;
                Exception? lastEx = null;
                while (version == null && sw.Elapsed < TimeSpan.FromSeconds(60))
                {
                    attempt++;
                    try
                    {
                        var versionUri = new Uri(baseUrl, "/api/v3/core/version");
                        logger.LogInformation("[AK] Version check attempt {Attempt} to {Uri}", attempt, versionUri);
                        version = client.GetVersion(baseUrl, token).GetAwaiter().GetResult();
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        var delayMs = Math.Min(8000, 250 * (int)Math.Pow(2, Math.Min(6, attempt - 1)));
                        logger.LogWarning(ex, "[AK] core/version failed (attempt {Attempt}), retrying in {Delay}ms", attempt, delayMs);
                        _ = webLogger?.Warning($"[AK] version check failed (attempt {attempt}), retry in {delayMs}ms: {ex.Message}");
                        Thread.Sleep(delayMs);
                    }
                }

                if (version == null)
                {
                    logger.LogError(lastEx, "[AK] Unable to contact Authentik API (core/version) after retries");
                    throw new InvalidOperationException("Authentik API is unavailable (core/version) after retries", lastEx);
                }

                logger.LogInformation("Authentik version: {Version}", version.Version ?? "unknown");
                _ = webLogger?.Information($"[AK] Version: {version.Version ?? "unknown"}");

                // Find a default authorization flow
                var flow = client.FindAuthorizationFlow(baseUrl, token).GetAwaiter().GetResult();
                if (flow == null)
                {
                    _ = webLogger?.Error("[AK] No default authorization flow found");
                    throw new InvalidOperationException("No default authorization flow found in Authentik.");
                }
                _ = webLogger?.Information($"[AK] Using authorization flow: pk={flow.Pk}, slug={flow.Slug}");
                logger.LogInformation("[AK] Authorization flow resolved: pk={Pk}, slug={Slug}", flow.Pk, flow.Slug);

                // Ensure provider
                _ = webLogger?.Information($"[AK] Checking provider by client_id: {clientId}");
                var provider = client.GetProviderByClientId(baseUrl, token, clientId).GetAwaiter().GetResult();
                if (provider == null)
                {
                    logger.LogInformation("Creating provider for client_id {ClientId}", clientId);
                    _ = webLogger?.Information($"[AK] Creating provider for client_id {clientId}");
                    var created = client.CreateProvider(baseUrl, token, new ProviderCreateDto
                    {
                        Name = $"{appName} OIDC",
                        ClientType = "confidential",
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        AuthorizationFlow = flow.Pk,
                        RedirectUris = redirectUris
                    }).GetAwaiter().GetResult();
                    provider = created;
                    _ = webLogger?.Information($"[AK] Provider created: pk={provider.Pk}, name={provider.Name}");
                    logger.LogInformation("[AK] Provider created: pk={Pk}, name={Name}, client_id={ClientId}, redirects={RedirectCount}", provider.Pk, provider.Name, provider.ClientId, provider.RedirectUris?.Length ?? 0);
                }
                else
                {
                    // Update redirect URIs or secret if changed
                    var needSecretUpdate = !string.IsNullOrWhiteSpace(clientSecret) && clientSecret != provider.ClientSecret;
                    var needRedirectUpdate = !provider.RedirectUris.SequenceEqual(redirectUris);
                    if (needSecretUpdate || needRedirectUpdate)
                    {
                        logger.LogInformation("Patching provider {Pk}: secret? {Secret}, redirect URIs? {Redirect}", provider.Pk, needSecretUpdate, needRedirectUpdate);
                        _ = webLogger?.Information($"[AK] Patching provider pk={provider.Pk}: secretUpdate={needSecretUpdate}, redirectUpdate={needRedirectUpdate}");
                        provider = client.PatchProvider(baseUrl, token, provider.Pk, new ProviderPatchDto
                        {
                            ClientSecret = needSecretUpdate ? clientSecret : null,
                            RedirectUris = needRedirectUpdate ? redirectUris : provider.RedirectUris
                        }).GetAwaiter().GetResult();
                        _ = webLogger?.Information($"[AK] Provider patched: pk={provider.Pk}");
                        logger.LogInformation("[AK] Provider patched: pk={Pk}, redirects={RedirectCount}", provider.Pk, provider.RedirectUris?.Length ?? 0);
                    }
                    // Prefer the provided secret to ensure we can authenticate
                    if (!string.IsNullOrWhiteSpace(clientSecret))
                        provider.ClientSecret = clientSecret;
                }

                // Ensure application
                _ = webLogger?.Information($"[AK] Checking application by slug: {slug}");
                var app = client.GetApplicationBySlug(baseUrl, token, slug).GetAwaiter().GetResult();
                if (app == null)
                {
                    logger.LogInformation("Creating application with slug {Slug}", slug);
                    _ = webLogger?.Information($"[AK] Creating application with slug {slug}");
                    app = client.CreateApplication(baseUrl, token, new ApplicationCreateDto
                    {
                        Name = appName,
                        Slug = slug,
                        Provider = provider.Pk
                    }).GetAwaiter().GetResult();
                    _ = webLogger?.Information($"[AK] Application created: pk={app.Pk}, slug={app.Slug}");
                    logger.LogInformation("[AK] Application created: pk={Pk}, slug={Slug}, provider={ProviderPk}", app.Pk, app.Slug, provider.Pk);
                }
                else if (app.Provider != provider.Pk)
                {
                    logger.LogInformation("Patching application {Pk} to link provider {ProviderPk}", app.Pk, provider.Pk);
                    _ = webLogger?.Information($"[AK] Relinking application pk={app.Pk} to provider pk={provider.Pk}");
                    app = client.PatchApplication(baseUrl, token, app.Pk, new ApplicationPatchDto
                    {
                        Provider = provider.Pk
                    }).GetAwaiter().GetResult();
                    _ = webLogger?.Information($"[AK] Application relinked: pk={app.Pk} -> provider pk={provider.Pk}");
                    logger.LogInformation("[AK] Application relinked: pk={Pk}, provider={ProviderPk}", app.Pk, provider.Pk);
                }

                // Finalize settings from ensured resources
                result.ClientId = provider.ClientId ?? clientId;
                result.ClientSecret = !string.IsNullOrWhiteSpace(clientSecret) ? clientSecret : (provider.ClientSecret ?? "");
                result.RedirectUris = redirectUris;
                logger.LogInformation("Bootstrap finished: slug={Slug}, client_id={ClientId}", result.Slug, result.ClientId);
                _ = webLogger?.Information($"[AK] Bootstrap finished: slug={result.Slug}, clientId={result.ClientId}, authority={result.Authority}");

                // Post-API verification: re-read provider and application and log results
                logger.LogInformation("[AK] Post-verify: checking resources via API...");
                var provider2 = client.GetProviderByClientId(baseUrl, token, clientId).GetAwaiter().GetResult();
                var app2 = client.GetApplicationBySlug(baseUrl, token, slug).GetAwaiter().GetResult();
                var providerOk = provider2 != null;
                var appOk = app2 != null && app2.Provider == (provider2?.Pk ?? -1);
                var redirectsOk = provider2 != null && provider2.RedirectUris.SequenceEqual(redirectUris);
                logger.LogInformation("[AK] Post-verify results: providerOk={ProviderOk}, appOk={AppOk}, redirectsOk={RedirectsOk}, providerPk={ProviderPk}, appPk={AppPk}",
                    providerOk, appOk, redirectsOk, provider2?.Pk, app2?.Pk);
                _ = webLogger?.Information($"[AK] Post-verify: providerOk={providerOk}, appOk={appOk}, redirectsOk={redirectsOk}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Authentik bootstrap failed; using configuration values where possible.");
                _ = webLogger?.Error(ex);
                _ = webLogger?.Error($"[AK] Bootstrap failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Парсит CSV/список Redirect URI из ENV/конфига.
        /// </summary>
        private static string[] ParseRedirectUris(string csv)
        {
            return csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => Uri.IsWellFormedUriString(s, UriKind.Absolute))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToArray();
        }

        /// <summary>
        /// Гарантирует наличие варианта /signin-oidc для каждого базового хоста.
        /// Это необходимо для стандартного обработчика OIDC в ASP.NET Core.
        /// </summary>
        private static string[] AugmentWithSigninOidc(string[] redirectUris)
        {
            var set = new HashSet<string>(redirectUris, StringComparer.OrdinalIgnoreCase);
            foreach (var uriStr in redirectUris)
            {
                if (!Uri.TryCreate(uriStr, UriKind.Absolute, out var uri)) continue;
                var basePart = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : ":" + uri.Port)}";
                var signin = basePart + "/signin-oidc";
                set.Add(signin);
            }
            return set.ToArray();
        }

        /// <summary>
        /// Криптографически безопасная генерация client_secret (hex-строка из случайных байт).
        /// </summary>
        private static string GenerateToken(int bytesLength)
        {
            // length is in bytes; return hex string twice that length
            var bytes = RandomNumberGenerator.GetBytes(bytesLength);
            return Convert.ToHexString(bytes);
        }

        /// <summary>
        /// Masks secrets for safe logging: keeps first 4 and last 4 chars.
        /// </summary>
        private static string Mask(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return "<empty>";
            if (secret.Length <= 8) return new string('*', secret.Length);
            return secret.Substring(0, 4) + new string('*', Math.Max(0, secret.Length - 8)) + secret.Substring(secret.Length - 4);
        }
    }
}
