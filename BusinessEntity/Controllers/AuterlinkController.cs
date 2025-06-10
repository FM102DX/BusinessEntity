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
            // Перенаправляем на новый AuthController
            _logger.LogInformation("Redirecting legacy login request to new AuthController");
            return RedirectToAction("Login", "Auth", new { returnUrl });
        }

        [HttpGet("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // Перенаправляем на новый AuthController
            _logger.LogInformation("Redirecting legacy logout request to new AuthController");
            return RedirectToAction("Logout", "Auth");
        }

        [HttpGet("callback")]
        public IActionResult Callback(string? code, string? state, string? error)
        {
            // Перенаправляем на новый AuthController
            _logger.LogInformation("Redirecting legacy callback request to new AuthController");
            return RedirectToAction("Callback", "Auth", new { code, state, error });
        }

        [HttpGet("logged-out")]
        public IActionResult LoggedOut()
        {
            // Перенаправляем на новый AuthController
            return RedirectToAction("LoggedOut", "Auth");
        }

        [HttpGet("processlogin")]
        public IActionResult ProcessLogin(string username, string? email = null, string? returnUrl = null)
        {
            // Этот метод больше не используется в JWT-авторизации
            _logger.LogWarning("Legacy processlogin endpoint called - redirecting to main auth flow");
            return RedirectToAction("Login", "Auth", new { returnUrl });
        }
    }
}