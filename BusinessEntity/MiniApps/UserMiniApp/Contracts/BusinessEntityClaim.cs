namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    // Представляет один claim пользователя в формате, удобном для mini-app и UI.
    public sealed record BusinessEntityClaim(
        string Type,
        string Value,
        string Issuer,
        string OriginalIssuer,
        string ValueType);
}
