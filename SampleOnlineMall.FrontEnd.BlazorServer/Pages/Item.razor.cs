using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using SampleOnlineMall.FrontEnd.BlazorServer.Components.ImageSelector;
using System.Collections;
using SampleOnlineMall.FrontEnd.BlazorServer.Data;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.Core.Mappers;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Pages

{
    public partial class Item : ComponentBase
    {

        [Inject]
        public StoreManager Manager { get; set; }

        [Inject]
        public Mapper Mpr { get; set; }

        [Inject]
        public Serilog.ILogger Logger { get; set; }

        [Inject]
        public NavigationManager Navigation { get; set; }

        [Parameter]
        public Guid ItemId { get; set; }

        public string ActualItemSupplierName
        {
            get
            {
                if (ActualItem.Supplier == null) return "Marketplace";
                return ActualItem.Supplier.Name;
            }
        }

        public List<SelectableImage> ImgList { get; set; }= new List<SelectableImage>();

        public CommodityItemFrontendDisplayed ActualItem { get; set; } = new CommodityItemFrontendDisplayed();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                ActualItem = await Manager.GetByIdOrNullAsync(ItemId);

                Logger.Information($"ActualItem.Name={ActualItem.Name}");
                
                List<PictureInfo> pictureinfo = ActualItem.Pictures.ToList();

                for (int i = 0; i < pictureinfo.Count; i++)
                {
                    ImgList.Add(new SelectableImage() { Id = i, FullSizePath = pictureinfo[i].MediumPictureFullPath, MidSizePath = pictureinfo[i].MediumPictureFullPath, ThumbPath = pictureinfo[i].SmallPictureFullPath });
                }

                Logger.Information($"ImgList.ItemsTotalCount={ImgList.Count} pictureinfo.count={pictureinfo.Count}");

                //SetCurrentImage();

                //SmallImgSelector.MySelectionChanged += SmallImgSelector_MySelectionChanged;
            }
            catch (Exception ex)
            {
                Logger.Error("GetItemError" + ex.Message);
            }
        }

        public string CurrentImageFullPath { get; set; }


        private SelectableImage SelectableImageFromPictureInfo(PictureInfo pictureInfo)
        {
            var img = new SelectableImage();
            img.ThumbPath = pictureInfo.SmallPictureFullPath;
            img.MidSizePath = pictureInfo.MediumPictureFullPath;
            img.FullSizePath = pictureInfo.BigPictureFullPath;
            return img;
        }
    }
}
