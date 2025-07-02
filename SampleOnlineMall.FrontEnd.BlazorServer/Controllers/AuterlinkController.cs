using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Controllers
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

            // В реальной реализации здесь должен быть редирект на Auterlink
            return View("Login", returnUrl);
        }

        [HttpPost("login")]
        public async Task<IActionResult> ProcessLogin(string username, string email, string? returnUrl = null)
        {
            // TODO: В реальной реализации здесь должна быть валидация токена от Auterlink
            
            if (string.IsNullOrEmpty(username))
            {
                ViewBag.Error = "Имя пользователя обязательно";
                return View("Login", returnUrl);
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

            // Перенаправляем на главную страницу вместо специальной страницы
            return Redirect("/");
        }

        [HttpGet("logged-out")]
        public IActionResult LoggedOut()
        {
            return View("LoggedOut");
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            // TODO: Обработка callback от Auterlink после успешной аутентификации
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError($"Auterlink authentication error: {error}");
                return RedirectToAction("Login");
            }

            // Здесь должна быть обработка кода авторизации от Auterlink
            // и получение токенов пользователя
            
            _logger.LogInformation("Auterlink callback received");
            
            return RedirectToAction("Login");
        }
    }
}