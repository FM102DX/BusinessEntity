using ControlNode.Data.AssortmentLoader;
using Microsoft.AspNetCore.Components;

namespace ControlNode.Pages
{
    public partial class Assortment
    {
        private string Message { get; set; } = "";
        private int AssortPosQ { get; set; } = 0;
        private List<string> Content{ get; set; } = new List<string>();


        [Inject]
        private AssortmentLoader AssortLoader { get; set; } // Инжектирование через DI
        public Assortment()
        {
            
        }
        protected override void OnInitialized()
        {
            if (AssortLoader != null)
            {
                AssortLoader.PropertyChanged += AssortLoader_PropertyChanged;
            }
        }
        private void AssortLoader_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AssortLoader.Status))
            {
                Message=AssortLoader.Status;
                StateHasChanged();
            }
            if (e.PropertyName == nameof(AssortLoader.Content))
            {
                Content = AssortLoader.Content.Split(';').ToList();
                StateHasChanged();
            }
        }

        private async void OnLoadAssortClick()
        {
            await AssortLoader.LoadAsync();
        }
    }
}
