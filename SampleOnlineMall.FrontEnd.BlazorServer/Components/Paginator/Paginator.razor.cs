using Microsoft.AspNetCore.Components;
using SampleOnlineMall.FrontEnd.BlazorServer.Data;
using SampleOnlineMall.WebLogger.Services;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator
{
    public partial class Paginator : ComponentBase
    {
        [Inject]
        IWebLoggerService Logger { get; set; }

        [Inject]
        NavigationManager Navi { get; set; }

        [Inject]
        ComponentHub CompHub { get; set; }

        [Parameter]
        public string? SearchText { get; set; }

        [Parameter]
        public SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator.PaginatorUsageCaseEnum UsageCase { get; set; }

        public bool FirstBtnsClickable => SelectedPage > 1;
        public bool LastBtnsClickable => SelectedPage < PagesCount;

        public string FirstBtnsClickableClass=>FirstBtnsClickable ?  "paginator-control-item-clickable" : "paginator-control-item-nonclickable";
        public string LastBtnsClickableClass => LastBtnsClickable ? "paginator-control-item-clickable" : "paginator-control-item-nonclickable";

        public string FistPageNo => "1";

        public int NextNumber { get; set; }
        public int PrevNumber { get; set; }

        public int SelectedPage { get; set; }

        public int ItemsPerPage { get; set; }

        public int TotalCount { get; set; }

        public int PagesCount { get; set; }

        public string Status { get; set; }

        public string StrConcat(string str1, string str2)
        {
            return str1 + str2;
        }

        public string SearchStringPart
        {
            get
            {
                if (UsageCase== PaginatorUsageCaseEnum.Regular)
                {
                    return "";
                }
                else if (UsageCase == PaginatorUsageCaseEnum.Search)
                {
                    return $"search/{SearchText}/";
                }
                else
                {
                    return "";
                }
            }
        }

        public string SelectedClass(int i)
        {
            string selectedClass = "";

            if (i == SelectedPage)
            {
                selectedClass = "paginator-control-item-selected";
            }
            else
            {
                selectedClass = "paginator-control-item-regular";
            }
            return selectedClass;
        }


        protected override void OnInitialized()
        {
            CompHub.PaginatorStateSet += CompHub_SetPaginatorState;
        }

        private void CompHub_SetPaginatorState(int selectedPage, int count, int itemsPerPage, SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator.PaginatorUsageCaseEnum usageCase)
        {
            SelectedPage = selectedPage == 0 ? 1 : selectedPage;
            TotalCount = count;
            ItemsPerPage = itemsPerPage;
            NextNumber = (SelectedPage == PagesCount) ? PagesCount : SelectedPage + 1;
            PrevNumber = (SelectedPage == 1) ? 1 : SelectedPage - 1;
            PagesCount = count / itemsPerPage;
            if (PagesCount * itemsPerPage < count) PagesCount++;
            if (PagesCount == 0) PagesCount = 1;
            if (SelectedPage == 0) SelectedPage = 1;
            UsageCase = usageCase;
            Status = $"[Paginator] selectedPage={SelectedPage},totalCount={TotalCount}, itemsPerPage={ItemsPerPage} PagesCount={PagesCount} NextNumber={NextNumber} PrevNumber={PrevNumber}";
            StateHasChanged();
        }
    }
}
