using Microsoft.AspNetCore.Components;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.ImageSelector
{
    public partial class ImageSelectorElement : ComponentBase
    {
        [Parameter]
        public SelectableImage Image { get; set; } = new SelectableImage();

        [Parameter]
        public int ThumbWidthPx { get; set; } = 0;

        [Parameter]
        public int ThumbHeightPx { get; set; } = 0;

        [Parameter]
        public ImageSelector Parent { get; set; } = null;

        public string SelctionClass { get => Image.Selected ? Parent.SelectedImageClass : Parent.RegularImageClass; }

        public string ThumbStyle
        {
            get {
                if (ThumbWidthPx != 0) { return $"Width: {ThumbWidthPx}px"; }
                if (ThumbHeightPx != 0){ return $"Heigh: {ThumbHeightPx}px"; }
                return "";
            }
        }
        public void ClickHandler()
        {
            Parent.ElementClicked(Image.Id);
        }
    }
}
