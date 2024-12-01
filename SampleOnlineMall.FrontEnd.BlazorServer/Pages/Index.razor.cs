using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SampleOnlineMall.FrontEnd.BlazorServer.Components.ShopItemCollection.ShopItemCollection;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Pages
{
    public partial class Index : ComponentBase
    {
        [Parameter]
        public int? Page { get; set; }

        public int PageToPass 
        { 
            get 
            {
                int page = 0;
                if(Page!=null)
                {
                    page = (int)Page;
                }
                return page;
            } 
        }

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
