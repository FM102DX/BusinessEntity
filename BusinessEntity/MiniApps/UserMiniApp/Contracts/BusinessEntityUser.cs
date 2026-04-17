using System.Security.Claims;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    public sealed record BusinessEntityUser(
        string UserId,
        string UserName,
        string? Email,
        bool IsAuthenticated,
        IReadOnlyList<string> Groups,
        IReadOnlyList<BusinessEntityClaim> Claims)
    {
        public bool HasGroup(string groupName)
        {
            return Groups.Any(group => string.Equals(group, groupName, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<string> GetClaimValues(string claimType)
        {
            return Claims
                .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string? GetFirstClaimValue(string claimType)
        {
            return Claims.FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public string? GetNameIdentifier()
        {
            return GetFirstClaimValue(ClaimTypes.NameIdentifier) ?? GetFirstClaimValue("sub");
        }
    }
}
