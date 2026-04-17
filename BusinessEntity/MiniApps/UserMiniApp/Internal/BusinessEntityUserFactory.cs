using System.Security.Claims;
using System.Text.Json;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.Services;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Преобразует текущий ClaimsPrincipal из Authentik в доменный BusinessEntityUser.
    internal sealed class BusinessEntityUserFactory
    {
        private readonly AuthentikSessionManager _authentikSessionManager;

        // Сохраняет доступ к текущей Authentik session для последующего построения пользователя.
        public BusinessEntityUserFactory(AuthentikSessionManager authentikSessionManager)
        {
            _authentikSessionManager = authentikSessionManager;
        }

        // Строит BusinessEntityUser из текущего principal и извлекает из claims группы и базовые поля.
        public async Task<BusinessEntityUser?> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var principal = await _authentikSessionManager.GetCurrentUserAsync();
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var claims = principal.Claims
                .Select(claim => new BusinessEntityClaim(
                    claim.Type,
                    claim.Value,
                    claim.Issuer,
                    claim.OriginalIssuer,
                    claim.ValueType))
                .ToList();

            var userId =
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? string.Empty;

            var userName =
                principal.Identity?.Name
                ?? claims.FirstOrDefault(claim => claim.Type == "preferred_username")?.Value
                ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?.Value
                ?? userId;

            var email =
                claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value
                ?? claims.FirstOrDefault(claim => claim.Type == "email")?.Value;

            // Нормализуем группы, даже если middleware отдаст их как JSON-массив в одном claim.
            var groups = ExtractGroups(claims);

            return new BusinessEntityUser(
                userId,
                userName,
                email,
                true,
                groups,
                claims);
        }

        // Извлекает группы пользователя из raw claims и приводит их к плоскому списку строк.
        private static IReadOnlyList<string> ExtractGroups(IEnumerable<BusinessEntityClaim> claims)
        {
            var groups = new List<string>();

            foreach (var claim in claims.Where(claim => string.Equals(claim.Type, "groups", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(claim.Value))
                {
                    continue;
                }

                var value = claim.Value.Trim();
                if (value.StartsWith("[", StringComparison.Ordinal))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<string>>(value);
                        if (parsed != null)
                        {
                            groups.AddRange(parsed.Where(group => !string.IsNullOrWhiteSpace(group)));
                            continue;
                        }
                    }
                    catch
                    {
                        // Keep the raw claim below if Authentik or middleware changes the format.
                    }
                }

                groups.Add(value);
            }

            return groups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
