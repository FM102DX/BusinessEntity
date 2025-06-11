namespace BusinessEntity.Services
{
    public record TokenResponseAuthenticCustom(
        string AccessToken,
        string IdToken,
        string? RefreshToken = null
    );
}