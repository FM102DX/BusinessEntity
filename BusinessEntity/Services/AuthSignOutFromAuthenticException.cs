using System;

namespace BusinessEntity.Services
{
    /// <summary>
    /// Исключение, возникающее при неудачном выходе из системы аутентификации Authentik
    /// </summary>
    public class AuthSignOutFromAuthenticException : Exception
    {
        public AuthSignOutFromAuthenticException() : base("Failed to sign out from Authentik authentication server")
        {
        }

        public AuthSignOutFromAuthenticException(string message) : base(message)
        {
        }

        public AuthSignOutFromAuthenticException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}