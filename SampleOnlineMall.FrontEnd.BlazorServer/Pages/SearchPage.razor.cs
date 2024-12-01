using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SampleOnlineMall.FrontEnd.BlazorServer.Components.ShopItemCollection.ShopItemCollection;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Pages
{
    public partial class SearchPage : ComponentBase
    {

        [Inject]
        public Serilog.ILogger Logger { get; set; }

        [Parameter]
        public string SearchText { get; set; }
        
        [Parameter]
        public int? Page { get; set; }
        
        public int PageToPass
        {
            get
            {
                int page = 0;
                if (Page != null)
                {
                    page = (int)Page;
                }
                return page;
            }
        }
        public ShopItemCollectionUsageCaseEnum UsageCase { get; set; } = ShopItemCollectionUsageCaseEnum.MainBarSearch;

        protected override async Task OnInitializedAsync()
        {

        }
    }
}
