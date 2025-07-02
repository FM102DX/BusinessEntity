using Microsoft.AspNetCore.Components;

namespace SampleOnlineMall.WebLogger.Blazor.Pages
{
    public partial class Index
    {
        [Inject]
        public Serilog.ILogger Logger { get; set; }


protected override async Task OnInitializedAsync()
        {

        }

        protected override void OnInitialized()
        {

        }

    }
}
