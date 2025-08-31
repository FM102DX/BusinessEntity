using BusinessEntity.Authentik;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BusinessEntity.Extensions
{
    /// <summary>
    /// Configures OpenID Connect (OIDC) for Authentik using settings produced at runtime
    /// by the bootstrap service. Keeps cookies for local sign-in and uses code flow.
    /// </summary>
    internal static class OidcConfigurator
    {
        /// <summary>
        /// Registers the OpenIdConnect handler with Authority/Client credentials from Authentik.
        /// </summary>
        public static IServiceCollection AddAuthentikOpenIdConnect(this IServiceCollection services, CreatedOidcSettings settings)
        {
            services.AddAuthentication()
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    // Authority points to Authentik application URL (/application/o/{slug}/)
                    options.Authority = settings.Authority;
                    // Client credentials ensured/returned by bootstrap
                    options.ClientId = settings.ClientId;
                    options.ClientSecret = settings.ClientSecret;
                    // Use Authorization Code Flow
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    // Allow HTTP for local/dev Authentik
                    options.RequireHttpsMetadata = false;
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    // Scopes
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    if (!options.Scope.Contains("roles")) options.Scope.Add("roles");

                    // Claims mapping
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        RoleClaimType = "roles"
                    };
                });

            return services;
        }
    }
}
