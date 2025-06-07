using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessEntity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        [HttpPost("signout")]
        [AllowAnonymous]
        public new async Task<IActionResult> SignOut()
        {
            try
            {
                if (HttpContext.User.Identity?.IsAuthenticated == true)
                {
                    var userName = HttpContext.User.Identity.Name;
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    _logger.LogInformation($"User {userName} successfully signed out");
                }
                else
                {
                    _logger.LogInformation("Anonymous user attempted to sign out");
                }

                return Ok(new { success = true, message = "Successfully signed out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while signing out user");
                return StatusCode(500, new { success = false, message = "Error during sign out" });
            }
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetAuthStatus()
        {
            var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated ?? false;
            var userName = HttpContext.User.Identity?.Name;
            
            return Ok(new { 
                isAuthenticated = isAuthenticated,
                userName = userName
            });
        }
    }
}