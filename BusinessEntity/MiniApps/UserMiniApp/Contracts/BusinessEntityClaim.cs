namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    public sealed record BusinessEntityClaim(
        string Type,
        string Value,
        string Issuer,
        string OriginalIssuer,
        string ValueType);
}
