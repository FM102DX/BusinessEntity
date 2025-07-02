using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Components
{
    public partial class UnauthorizedRedirect : ComponentBase
    {
        [Inject] public ILogger<UnauthorizedRedirect> Logger { get; set; } = default!;

        protected override Task OnInitializedAsync()
        {
            Logger.LogInformation("Displaying unauthorized access message to user");
            return Task.CompletedTask;
        }
    }
}