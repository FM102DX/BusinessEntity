using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Pages
{
    public partial class Unauthorized : ComponentBase
    {
        [Inject] public IAuterlinkAuthService AuthService { get; set; } = default!;
        [Inject] public ILogger<Unauthorized> Logger { get; set; } = default!;

        protected string LoginUrl => AuthService.GetLoginUrl();

        protected override async Task OnInitializedAsync()
        {
            Logger.LogWarning("User accessed unauthorized page - redirecting to Auterlink login");
            await Task.CompletedTask;
        }
    }
}