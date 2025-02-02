using SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator;
using SampleOnlineMall.WebLogger.Services;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Data
{
    public class ComponentHub
    {
        //class to organize component interaction

        private IWebLoggerService _webLogger;
        
        public string SearchText { get; set; }

        public ComponentHub(IWebLoggerService webLogger)
        {
            _webLogger = webLogger;
        }
        
        //search
        public void Search (string SearchText)
        {
            DoingSearch(SearchText);
        }

        public event DoingSearchHandler DoingSearch;

        public delegate void DoingSearchHandler(string SearchText);

        // PaginatorStateSet
        public delegate void SetPaginatorStateHandler(int selectedPage, int count, int itemsPerPage, SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator.PaginatorUsageCaseEnum usageCase);

        public event SetPaginatorStateHandler PaginatorStateSet;

        public void SetPaginatonState(int selectedPage, int count, int itemsPerPage, SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator.PaginatorUsageCaseEnum usageCase)
        {
            if(PaginatorStateSet!=null)
                PaginatorStateSet (selectedPage,count, itemsPerPage, usageCase);
        }
    }
}
