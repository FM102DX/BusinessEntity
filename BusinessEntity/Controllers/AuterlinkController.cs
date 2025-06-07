using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BusinessEntity.Controllers
{
    [Route("auterlink")]
    public class AuterlinkController : Controller
    {
        private readonly ILogger<AuterlinkController> _logger;

        public AuterlinkController(ILogger<AuterlinkController> logger)
        {
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            // TODO: Здесь должна быть интеграция с реальным Auterlink
            // Пока что создаем тестового пользователя для демонстрации
            
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(returnUrl ?? "/");
            }

            // Перенаправляем на Razor страницу вместо View
            var loginUrl = "/auterlink/login";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                loginUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
            }
            
            return Redirect(loginUrl);
        }

        [HttpGet("processlogin")]
        public async Task<IActionResult> ProcessLogin(string username, string? email = null, string? returnUrl = null)
        {
            // TODO: В реальной реализации здесь должна быть валидация токена от Auterlink
            
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Login attempt with empty username");
                var errorUrl = "/auterlink/login";
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    errorUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
                }
                return Redirect(errorUrl);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            };

            if (!string.IsNullOrEmpty(email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation($"User {username} logged in successfully");

            return LocalRedirect(returnUrl ?? "/");
        }

        [HttpGet("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            _logger.LogInformation($"User {userName} logged out");

            // TODO: В реальной реализации здесь может быть редирект на Auterlink для глобального выхода
            
            return Redirect("/auterlink/loggedout");
        }

        [HttpGet("logged-out")]
        public IActionResult LoggedOut()
        {
            return Redirect("/auterlink/loggedout");
        }

        [HttpGet("callback")]
        public Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            // TODO: Обработка callback от Auterlink после успешной аутентификации
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError($"Auterlink authentication error: {error}");
                return Task.FromResult<IActionResult>(RedirectToAction("Login"));
            }

            // Здесь должна быть обработка кода авторизации от Auterlink
            // и получение токенов пользователя
            
            _logger.LogInformation("Auterlink callback received");
            
            return Task.FromResult<IActionResult>(RedirectToAction("Login"));
        }
    }
}