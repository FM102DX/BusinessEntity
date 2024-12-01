using SampleOnlineMall.FrontEnd.BlazorServer.Components.Paginator;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Data
{
    public class ComponentHub
    {

        Serilog.ILogger _logger;

        //class to organize component interactio
        public string SearchText { get; set; }

        public ComponentHub(Serilog.ILogger logger)
        {
            _logger = logger;
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
            PaginatorStateSet (selectedPage,count, itemsPerPage, usageCase);
        }
    }
}
