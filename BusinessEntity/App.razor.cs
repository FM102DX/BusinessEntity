using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace BusinessEntity
{
    public partial class App : ComponentBase
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        protected static string GetReturnUrl(RouteData routeData)
        {
            var returnUrl = $"/{routeData.PageType.Name}";
            if (routeData.RouteValues.Any())
            {
                var routeParams = string.Join("/", routeData.RouteValues.Values);
                returnUrl = $"/{routeParams}";
            }
            return returnUrl;
        }

        protected void RedirectToUnauthorized()
        {
            Navigation.NavigateTo("/unauthorized", true);
        }
    }
}