using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;
using SampleOnlineMall.WebLogger.Services;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.ImageSelector
{
    public partial class ImageSelector : ComponentBase
    {
        [Inject]
        public IWebLoggerService Logger { get; set; }

        [Parameter]
        public List<SelectableImage> Items { get; set; } = new List<SelectableImage>();

        [Parameter]
        public SelectorDirectionEnum SelectorDirection { get; set; } = SelectorDirectionEnum.Horizontal;

        [Parameter]
        public string HolderDivClass { get; set; }

        [Parameter]
        public string SelectedImageClass { get; set; } = "thumb-image-wrap_selected";

        [Parameter]
        public string RegularImageClass { get; set; } = "thumb-image-wrap_regular";
        
        [Parameter]
        public IImageSelectorClient Parent { get; set; }

        [Parameter]
        public SelectableImage Current { get; set; } = null;

        public enum SelectorDirectionEnum
        {
            Vertical = 1,
            Horizontal = 2
        }
        protected override void OnInitialized()
        {

        }
        protected override void OnParametersSet()
        {
            Logger.Information($"This is ImageSelector, Items has {Items.Count} elements");
            
            if(Items.Count > 0)
            {
                if (string.IsNullOrEmpty(Current.MidSizePath))
                {
                    ElementClicked(Items[0].Id);
                }
            }
        }
        
        private void UpdateImageSet()
        {

        }

        public void ElementClicked(int elementId)
        {
            Logger.Information($"ImageSelector: Element clicked, elementId={elementId}");
            Items.ForEach(x => x.Selected = false);
            var item = Items.Where(x=>x.Id== elementId).FirstOrDefault();
            item.Selected = true;
            Parent.SelectionChanged(item);
            //StateHasChanged();
        }

    }
}
