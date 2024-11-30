using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using SampleOnlineMall.FrontEnd.Blazor.Components.ImageSelector;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.DisplayImageWithSelector
{
    public partial class DisplayImageWithSelector : ComponentBase, IImageSelectorClient
    {

        [Inject]
        public Serilog.ILogger Logger { get; set; }

        [Parameter]
        public int ContainerWidth { get; set; }
        
        [Parameter]
        public int ContainerHeight { get; set; }

        [Parameter]
        public SelectorPositionEnum SelectorPosition { get; set; }=SelectorPositionEnum.bottom;

        [Parameter]
        public List<SelectableImage> ImgList { get; set; }=new List<SelectableImage>();

        private bool IsVerticalOrder=> SelectorPosition==SelectorPositionEnum.bottom;
        public int SelectorToImageRatioPercent { get; set; } = 20;
        public SelectableImage CurrentImage { get; set; }=new SelectableImage();
        public int SelectorHeight
        {
            get
            {
                if(SelectorPosition == SelectorPositionEnum.left)
                {
                    return ContainerHeight;
                }
                else if (SelectorPosition == SelectorPositionEnum.bottom)
                {
                    var x = RatioResult(ContainerHeight, SelectorToImageRatioPercent);
                    return x[0];
                }
                else
                {
                    return 0;
                }
            }
        }
        public int SelectorWidth
        {
            get
            {
                if (SelectorPosition == SelectorPositionEnum.left)
                {
                    var x = RatioResult(ContainerWidth, SelectorToImageRatioPercent);
                    return x[0];
                    
                }
                else if (SelectorPosition == SelectorPositionEnum.bottom)
                {
                    return ContainerWidth;
                }
                else
                {
                    return 0;
                }
            }
        }
        public int ImageHeight
        {
            get
            {
                if (SelectorPosition == SelectorPositionEnum.left)
                {
                    return ContainerHeight;
                }
                else if (SelectorPosition == SelectorPositionEnum.bottom)
                {
                    return ContainerHeight - SelectorHeight;
                }
                else
                {
                    return 0;
                }
            }
        }
        public int ImageWidth
        {
            get
            {
                if (SelectorPosition == SelectorPositionEnum.left)
                {
                    return ContainerWidth-SelectorWidth;
                }
                else if (SelectorPosition == SelectorPositionEnum.bottom)
                {
                    return ContainerWidth;
                }
                else
                {
                    return 0;
                }
            }
        }
        public string ContainerStyle => $"width:{ContainerWidth}px; height:{ContainerHeight}px;";
        public string ImageStyle => $"width:{ImageWidth}px; height:{ImageHeight}px;";
        public string SelectorStyle => $"width:{SelectorWidth}px; height:{SelectorHeight}px;";

        public enum SelectorPositionEnum
        {
            left=1,
            bottom=2
        }
        private int[] RatioResult(int whole, int ratioPercent)
        {
            var x = new int[2];
            x[0] = (whole / 100) * ratioPercent;
            x[1] = whole - x[0];
            return x;
        }

        protected override void OnInitialized()
        {
            
        }

        public void SelectionChanged(SelectableImage img)
        {
            CurrentImage = img;
            Logger.Information($"DisplayImageWithSelector: Selection changed, new id={img.Id}");
            StateHasChanged();
        }

        public void LogClickHandler()
        {
            Logger.Information($"Log click: CurrentImage.Id={CurrentImage.Id}");
            StateHasChanged();
        }
    }
}
