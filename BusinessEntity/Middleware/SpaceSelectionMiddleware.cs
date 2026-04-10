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
                var repository = context.RequestServices.GetRequiredService<IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity>>();
                var spaces = await repository.GetAllAsync(e => e.EntityType == BusinessEntityTypeEnum.Space, ct: context.RequestAborted);

                var defaultSpace = spaces
                    .OrderByDescending(space => string.Equals(space.Name, "Документы", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(space => space.Name)
                    .FirstOrDefault();

                if (defaultSpace != null)
                {
                    userContext.SetSpace(defaultSpace.Id, defaultSpace.Name);
                    _logger.LogInformation("Auto-selected default space {SpaceName} ({SpaceId}) for authenticated user", defaultSpace.Name, defaultSpace.Id);
                }
                else
                {
                    _logger.LogDebug("No spaces available, redirecting to /space-selection");
                    context.Response.Redirect("/space-selection");
                    return;
                }
            }

            await _next(context);
        }
    }
} 
