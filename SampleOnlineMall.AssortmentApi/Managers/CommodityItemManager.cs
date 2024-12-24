using SampleOnlineMall.Core.Appilcation;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using Shim= SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Formats;
using SampleOnlineMall.Core.Mappers;

namespace SampleOnlineMall.Core.Managers
{
    public class CommodityItemManager
    {
        private IAsyncRepository<CommodityItem> _repo;
        private Serilog.ILogger _logger;
        private SampleOnlineMallAssortmentApiApp _app;
        private CustomMapper _mapper;
        private WebLoggerManager _webLogger;
        public CommodityItemManager(IAsyncRepository<CommodityItem> repo, WebLoggerManager webLogger, Serilog.ILogger logger, SampleOnlineMallAssortmentApiApp app, CustomMapper mapper)
        {
            //_logger.Information($"CommodityItemManager_entering_ctor_0");
            _repo = repo;
            _logger = logger;
            _app = app;
            _mapper = mapper;
            _webLogger = webLogger;
        }

        public async Task<IEnumerable<CommodityItem>> GetAll()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<int> Count()
        {
            return await _repo.GetCountAsync();
        }

        public async Task<CommonOperationResult> DeleteItemById(Guid id)
        {
            try
            {
                await _repo.DeleteAsync(id);
                var imgPath = _app.GetCommodityItemImageDirectoryFromGuid(id);
                var exists = Directory.Exists(imgPath);
                _logger.Information($"trying to delete {imgPath}, it exists: {exists}");
                if (exists)
                {
                    Directory.Delete(imgPath,true);
                }
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"{ex.Message}");
            }
        }

        public async Task<CommonOperationResult> DeleteAll()
        {
            try
            {
                var itemList = await _repo.GetAllAsync();
                var itemGuidArray = itemList.ToList().Select(x => x.Id).ToArray();
                foreach(var id in itemGuidArray)
                {
                    await DeleteItemById(id);
                }
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"{ex.Message}");

            }
        }

        public async Task<CommonOperationResult> InsertFromWebApi (CommodityItemApiFeed item)
        {
            _logger.Information($"This is ItemManager. Received commodity item name={item.Name}");
            _webLogger.Log($"inserting item name={item.Name}");
            try
            {
                var exists = await _repo.Exists(item.Id);
                _logger.Information($"Checking item existance name={item.Name}, result: {exists}");
                if (exists)
                {
                    return CommonOperationResult.SayFail($"Unable to insert assortment item with name={item.Name}");
                }

                //saving object
                var convertedItem = _mapper.CommodityItemFromCommodityItemApiFeed(item);
                var saveRez = await _repo.AddAsync(convertedItem);

                if(!saveRez.Success) return CommonOperationResult.SayFail(saveRez.Message);

                // saving pictures
                byte[] imageByteArr;
                var picDirectory = _app.GetCommodityItemImageDirectoryFromGuid(item.Id);
                
                var firstPicPath = Path.Combine(picDirectory, "1.jpg");
                var secondPicPath = Path.Combine(picDirectory, "2.jpg");
                var thirdPicPath = Path.Combine(picDirectory, "3.jpg");

                var firstPicPathM = Path.Combine(picDirectory, "1m.jpg");
                var secondPicPathM = Path.Combine(picDirectory, "2m.jpg");
                var thirdPicPathM = Path.Combine(picDirectory, "3m.jpg");

                var firstPicPathS = Path.Combine(picDirectory, "1s.jpg");
                var secondPicPathS = Path.Combine(picDirectory, "2s.jpg");
                var thirdPicPathS= Path.Combine(picDirectory, "3s.jpg");

                //pic1
                if (!string.IsNullOrEmpty(item.FirstPic))
                {
                    imageByteArr = Convert.FromBase64String(item.FirstPic);
                    File.WriteAllBytes(firstPicPath, imageByteArr);
                    ResizeImageToWidthAndSave(firstPicPath, firstPicPathM, 700);
                    ResizeImageToWidthAndSave(firstPicPath, firstPicPathS, 300);
                }

                //pic2
                if (!string.IsNullOrEmpty(item.SecondPic))
                {
                    imageByteArr = Convert.FromBase64String(item.SecondPic);
                    File.WriteAllBytes(secondPicPath, imageByteArr);
                    ResizeImageToWidthAndSave(secondPicPath, secondPicPathM, 700);
                    ResizeImageToWidthAndSave(secondPicPath, secondPicPathS, 300);
                }

                //pic3
                if (!string.IsNullOrEmpty(item.ThirdPic))
                {
                    imageByteArr = Convert.FromBase64String(item.ThirdPic);
                    File.WriteAllBytes(thirdPicPath, imageByteArr);
                    ResizeImageToWidthAndSave(thirdPicPath, thirdPicPathM, 700);
                    ResizeImageToWidthAndSave(thirdPicPath, thirdPicPathS, 300);
                }
                item.FirstPic = null;
                item.SecondPic = null;
                item.ThirdPic = null;

                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"Ex={ex.Message} InnerEx={ex.InnerException}");
            }
        }

        public void ResizeImageToWidthAndSave(string source, string target, int targetWidth)
        {
            var img = Shim.Load(source);
            var format = img.Metadata.DecodedImageFormat;
            var resizedImage = ResizeImageToWidth(img, targetWidth);
            var memoryStream = new MemoryStream();
            resizedImage.Save(memoryStream, format);
            File.WriteAllBytes(target, memoryStream.ToArray());
        }

        public Shim ResizeImageToWidth(Shim source, int targetWidth)
        {
            double ratio = 0;
            if (targetWidth!=0)
            {
                ratio= (double) source.Width / targetWidth;
            }

            var targetHeightVar = source.Height / ratio;
            _webLogger.Log($"source.Width={source.Width} ratio={ratio} targetWidth={targetWidth} targetHeightVar ={targetHeightVar}");
            int targetHeight32 = Convert.ToInt32(targetHeightVar);
            SixLabors.ImageSharp.Size targetSize = new SixLabors.ImageSharp.Size(targetWidth, targetHeight32);
            source.Mutate(x => x.Resize(targetSize));
            return source;
        }
    }
}
