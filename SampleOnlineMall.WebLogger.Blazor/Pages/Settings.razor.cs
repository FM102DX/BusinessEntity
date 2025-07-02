using Microsoft.AspNetCore.Components;
using SampleOnlineMall.Core;
using WebLogger.Blazor.Core.Managers;


namespace SampleOnlineMall.WebLogger.Blazor.Pages
{
    public partial class Settings
    {
        [Inject]
        public Serilog.ILogger Logger { get; set; }

        [Inject]
        public WebLoggerSettingsManagerClient ItemManager { get; set; }

        public WebLoggerSettings Item { get; set; }
        public string ItemTxt { get; set; }

        protected override async Task OnInitializedAsync()
        {
            //Item = await ItemManager.GetOne();
            ItemTxt = await ItemManager.GetAsJson();
        }

        protected override void OnInitialized()
        {

        }

    }
}
