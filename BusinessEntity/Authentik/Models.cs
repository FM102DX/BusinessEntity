using System.Text.Json.Serialization;

/*
 DTOs used by Authentik REST API v3.
 - VersionDto: /api/v3/core/version
 - PagedResult<T>: generic paged response wrapper
 - FlowDto: minimal flow info (pk, slug) to locate default authorization flow
 - ProviderDto/CreateDto/PatchDto: OAuth2 provider data and mutations
 - ApplicationDto/CreateDto/PatchDto: Application data and mutations
 - CreatedOidcSettings: final OIDC settings emitted by bootstrap for .NET app
*/

namespace BusinessEntity.Authentik
{
    /// <summary>
    /// Version info returned by /api/v3/core/version
    /// </summary>
    internal class VersionDto
    {
        [JsonPropertyName("version")] public string? Version { get; set; }
    }

    /// <summary>
    /// Generic paged response container for Authentik list endpoints.
    /// </summary>
    internal class PagedResult<T>
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("results")] public List<T> Results { get; set; } = new();
    }

    /// <summary>
    /// Minimal flow representation (used to locate default authorization flow).
    /// </summary>
    internal class FlowDto
    {
        [JsonPropertyName("pk")] public int Pk { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
    }

    /// <summary>
    /// OAuth2/OIDC provider representation.
    /// </summary>
    internal class ProviderDto
    {
        [JsonPropertyName("pk")] public int Pk { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("client_id")] public string? ClientId { get; set; }
        [JsonPropertyName("client_secret")] public string? ClientSecret { get; set; }
        [JsonPropertyName("redirect_uris")] public string[] RedirectUris { get; set; } = System.Array.Empty<string>();
    }

    /// <summary>
    /// Payload for creating an OAuth2/OIDC provider.
    /// </summary>
    internal class ProviderCreateDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("client_type")] public string ClientType { get; set; } = "confidential";
        [JsonPropertyName("client_id")] public string ClientId { get; set; } = string.Empty;
        [JsonPropertyName("client_secret")] public string ClientSecret { get; set; } = string.Empty;
        [JsonPropertyName("authorization_flow")] public int AuthorizationFlow { get; set; }
        [JsonPropertyName("redirect_uris")] public string[] RedirectUris { get; set; } = System.Array.Empty<string>();
        [JsonPropertyName("client_authentication")] public string? ClientAuthentication { get; set; }
    }

    /// <summary>
    /// Payload for patching provider fields (redirect URIs and/or client secret).
    /// </summary>
    internal class ProviderPatchDto
    {
        [JsonPropertyName("redirect_uris")] public string[] RedirectUris { get; set; } = System.Array.Empty<string>();
        [JsonPropertyName("client_secret")] public string? ClientSecret { get; set; }
    }

    /// <summary>
    /// Authentik Application representation.
    /// </summary>
    internal class ApplicationDto
    {
        [JsonPropertyName("pk")] public int Pk { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("provider")] public int? Provider { get; set; }
    }

    /// <summary>
    /// Payload for creating an Authentik Application.
    /// </summary>
    internal class ApplicationCreateDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
        [JsonPropertyName("provider")] public int Provider { get; set; }
    }

    /// <summary>
    /// Payload for updating Application linkage to a provider.
    /// </summary>
    internal class ApplicationPatchDto
    {
        [JsonPropertyName("provider")] public int Provider { get; set; }
    }

    /// <summary>
    /// Final OIDC settings produced by bootstrap and consumed by .NET OIDC config.
    /// </summary>
    internal class CreatedOidcSettings
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string[] RedirectUris { get; set; } = System.Array.Empty<string>();
        public string Slug { get; set; } = string.Empty;
    }
}
