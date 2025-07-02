using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;

namespace BusinessEntity.Pages
{
    [AllowAnonymous]
    public partial class Logout : ComponentBase
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        protected override void OnInitialized()
        {
            // Перенаправляем на новую Razor страницу выхода
            Navigation.NavigateTo("/auterlink/loggedout", true);
        }
    }
} 