using BusinessEntity.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;

namespace BusinessEntity.Services
{
    public class UserContextService : IUserContextService
    {
        public const string CookieSpaceId = "be_selected_space_id";
        public const string CookieSpaceName = "be_selected_space_name";
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserContextService>? _logger;
        public UserContextService(IHttpContextAccessor httpContextAccessor, ILogger<UserContextService>? logger = null)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            try
            {
                var ctx = _httpContextAccessor.HttpContext;
                var reqCookies = ctx?.Request?.Cookies;
                if (reqCookies != null)
                {
                    if (reqCookies.TryGetValue(CookieSpaceId, out var sid) && Guid.TryParse(sid, out var gid))
                    {
                        CurrentSpaceId = gid;
                        reqCookies.TryGetValue(CookieSpaceName, out var sname);
                        CurrentSpaceName = sname;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read selected space cookies");
            }
        }

        public Guid? CurrentSpaceId { get; private set; }
        public string? CurrentSpaceName { get; private set; }
        public bool HasSelectedSpace => CurrentSpaceId.HasValue;

        public event Action<Guid?> SelectedSpaceChanged;

        public void SetSpace(Guid id, string name)
        {
            CurrentSpaceId = id;
            CurrentSpaceName = name;
            SelectedSpaceChanged?.Invoke(id);

            try
            {
                var ctx = _httpContextAccessor.HttpContext;
                if (ctx != null)
                {
                    var opts = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Path = "/"
                    };
                    ctx.Response.Cookies.Append(CookieSpaceId, id.ToString(), opts);
                    ctx.Response.Cookies.Append(CookieSpaceName, name ?? string.Empty, opts);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to write selected space cookies");
            }
        }

        public void ClearSpace()
        {
            CurrentSpaceId = null;
            CurrentSpaceName = null;
            SelectedSpaceChanged?.Invoke(null);

            try
            {
                var ctx = _httpContextAccessor.HttpContext;
                if (ctx != null)
                {
                    ctx.Response.Cookies.Delete(CookieSpaceId);
                    ctx.Response.Cookies.Delete(CookieSpaceName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete selected space cookies");
            }
        }
    }
}