using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using SampleOnlineMall.FrontEnd.Blazor.Data;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.Search
{
    public partial class Search : ComponentBase
    {
        [Inject]
        public Serilog.ILogger Logger { get; set; }

        [Inject]
        public  StoreManager MyStoreManager { get; set; }

        [Inject]
        public NavigationManager Navi { get; set; }

        [Inject]
        public ComponentHub CompHub { get; set; }

        [Inject]
        public NavigationManager Nav { get; set; }

        public string SearchBarElementClass { get; set; } = "SearchBarElement";
        
        public string DefaultSearchText { get; set; } = "Search here";

        public bool FieldHasText { get; set; } = false;

        public string Value { get; set; } = "";

        public void SetSearchBarElementClass()
        {
            if (string.IsNullOrEmpty(Value.Trim()) | Value == DefaultSearchText) { SearchBarElementClass = "SearchBarElement"; } else { SearchBarElementClass = "SearchBarElement CloseCrossWithImage"; }
            // StateHasChanged();
        }
        public void ResetSearchBarElementClass()
        {
             SearchBarElementClass = "SearchBarElement";
        }

        public void CloseCrossClicked()
        {
            Navi.NavigateTo(Navi.BaseUri);
        }

        public void KeypressHandler(KeyboardEventArgs e)
        {
            SetSearchBarElementClass();
        }

        public void SearchClicked()
        {
             ProcessSearch(Value);
        }
        public void EnterHandler(KeyboardEventArgs e)
        {
            Logger.Information($"Keypress {e.Key} {e.Code}");

            if (e.Code == "Enter" || e.Code == "NumpadEnter")
            {
                Logger.Information($"Keypress {e.Key} {e.Code} Value={Value}");
                ProcessSearch(Value);
            }
            else if(e.Code == "Escape")
            {
                Navi.NavigateTo(Navi.BaseUri);
            }
        }

        public void ProcessSearch(string SearchText)
        {
            if (string.IsNullOrEmpty(Value)) return;
            Logger.Information($"Performing search with text={SearchText}");
            
            Navi.NavigateTo(Navi.BaseUri + $@"search\{SearchText}");
            
            CompHub.Search(SearchText);
        }
        protected override void OnInitialized()
        {
            Nav.LocationChanged += (o, e) => {
                if (e.Location == Nav.BaseUri)
                {
                    Value = "";
                    ResetSearchBarElementClass();
                    StateHasChanged();
                }
            };
        }

    }
}
