using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;

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
        public static CreatedOidcSettings Ensure(string appName, IConfiguration configuration, ILogger logger)
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

            if (!ensure)
            {
                logger.LogInformation("EnsureAuthentikOnStartup=false; skipping bootstrap. Using configuration values for OIDC.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(baseUrlStr) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "Authentik bootstrap requires AUTHENTIK_BASE_URL and AUTHENTIK_API_TOKEN. Set them or disable EnsureAuthentikOnStartup=false.");
            }

            try
            {
                var baseUrl = new Uri(baseUrlStr.TrimEnd('/') + "/");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var client = new AuthentikClient(http);

                // Version check
                var version = client.GetVersion(baseUrl, token).GetAwaiter().GetResult();
                logger.LogInformation("Authentik version: {Version}", version.Version ?? "unknown");

                // Find a default authorization flow
                var flow = client.FindAuthorizationFlow(baseUrl, token).GetAwaiter().GetResult();
                if (flow == null)
                {
                    throw new InvalidOperationException("No default authorization flow found in Authentik.");
                }

                // Ensure provider
                var provider = client.GetProviderByClientId(baseUrl, token, clientId).GetAwaiter().GetResult();
                if (provider == null)
                {
                    logger.LogInformation("Creating provider for client_id {ClientId}", clientId);
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
                }
                else
                {
                    // Update redirect URIs or secret if changed
                    var needSecretUpdate = !string.IsNullOrWhiteSpace(clientSecret) && clientSecret != provider.ClientSecret;
                    var needRedirectUpdate = !provider.RedirectUris.SequenceEqual(redirectUris);
                    if (needSecretUpdate || needRedirectUpdate)
                    {
                        logger.LogInformation("Patching provider {Pk}: secret? {Secret}, redirect URIs? {Redirect}", provider.Pk, needSecretUpdate, needRedirectUpdate);
                        provider = client.PatchProvider(baseUrl, token, provider.Pk, new ProviderPatchDto
                        {
                            ClientSecret = needSecretUpdate ? clientSecret : null,
                            RedirectUris = needRedirectUpdate ? redirectUris : provider.RedirectUris
                        }).GetAwaiter().GetResult();
                    }
                    // Prefer the provided secret to ensure we can authenticate
                    if (!string.IsNullOrWhiteSpace(clientSecret))
                        provider.ClientSecret = clientSecret;
                }

                // Ensure application
                var app = client.GetApplicationBySlug(baseUrl, token, slug).GetAwaiter().GetResult();
                if (app == null)
                {
                    logger.LogInformation("Creating application with slug {Slug}", slug);
                    app = client.CreateApplication(baseUrl, token, new ApplicationCreateDto
                    {
                        Name = appName,
                        Slug = slug,
                        Provider = provider.Pk
                    }).GetAwaiter().GetResult();
                }
                else if (app.Provider != provider.Pk)
                {
                    logger.LogInformation("Patching application {Pk} to link provider {ProviderPk}", app.Pk, provider.Pk);
                    app = client.PatchApplication(baseUrl, token, app.Pk, new ApplicationPatchDto
                    {
                        Provider = provider.Pk
                    }).GetAwaiter().GetResult();
                }

                // Finalize settings from ensured resources
                result.ClientId = provider.ClientId ?? clientId;
                result.ClientSecret = !string.IsNullOrWhiteSpace(clientSecret) ? clientSecret : (provider.ClientSecret ?? "");
                result.RedirectUris = redirectUris;
                logger.LogInformation("Bootstrap finished: slug={Slug}, client_id={ClientId}", result.Slug, result.ClientId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Authentik bootstrap failed; using configuration values where possible.");
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
    }
}
