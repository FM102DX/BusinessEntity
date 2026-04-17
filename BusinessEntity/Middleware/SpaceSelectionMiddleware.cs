using BusinessEntity.Contracts;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Middleware
{
    public class SpaceSelectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SpaceSelectionMiddleware> _logger;

        private static readonly string[] _bypassPaths = new[]
        {
            "/space-selection",
            "/diagnostics",
            "/auth",
            "/_blazor",
            "/swagger",
            "/favicon",
            "/css",
            "/js",
            "/static",
            "/api"
        };

        public SpaceSelectionMiddleware(RequestDelegate next, ILogger<SpaceSelectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userContext = context.RequestServices.GetRequiredService<IUserContextService>();
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            bool shouldBypass = _bypassPaths.Any(p => path.StartsWith(p)) || path.Contains('.');

            // Редиректуем только если пользователь уже аутентифицирован
            bool isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;

            if (isAuthenticated && !shouldBypass && !userContext.HasSelectedSpace)
            {
                // Если пространство не восстановилось из cookies, всегда отправляем пользователя на явный выбор.
                _logger.LogInformation("Authenticated user has no selected space in cookies/context, redirecting to /space-selection");
                context.Response.Redirect("/space-selection");
                return;
            }

            await _next(context);
        }
    }
} 
