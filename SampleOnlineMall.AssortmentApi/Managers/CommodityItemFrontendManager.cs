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
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess.Models;
using Newtonsoft.Json;

namespace SampleOnlineMall.Core.Managers
{
    //used to show items in frontend
    public class CommodityItemFrontendManager
    {
        private IAsyncRepository<CommodityItem> _repo;
        private Serilog.ILogger _logger;
        private SampleOnlineMallAssortmentApiApp _app;
        
        private CustomMapper _mapper;
        private IAsyncRepository<Supplier> _supplierRepo;

        public CommodityItemFrontendManager(IAsyncRepository<CommodityItem> repo, IAsyncRepository<Supplier> supplierRepo,  Serilog.ILogger logger, SampleOnlineMallAssortmentApiApp app, CustomMapper mapper)
        {
            _repo = repo;
            _logger = logger;
            _app = app;
            _mapper = mapper;
            
            _supplierRepo = supplierRepo;
        }

        public async Task<IEnumerable<CommodityItemFrontend>> GetAll()
        {
            var items = (await _repo.GetAllAsync()).Select(x => _mapper.CommodityItemFrontendFromCommodityItem(x)).ToList();
            foreach (var item in items)
            {
                var x = GetPictureInfoListForItem(item);
                item.Pictures = x;
            }
           // _webLogMgr.Log($"{items[0].Pictures.Count()}");
            return items;
        }

        public async Task<RepositoryResponce<CommodityItemFrontend>> GetAllByRequest(RepositoryRequestTextSearch repositoryRequest)
        {
            RepositoryResponce<CommodityItem> sourceResponce = null;
            var searchText = repositoryRequest.SearchText?.ToLower();
            if (repositoryRequest.UseSearch)
            {
                //_webLogMgr.Log($"[CommodityItemFrontendManager]: used search");
                var searchReq = RepositoryRequestFuncSearch<CommodityItem>.FromTextSearchRequest(repositoryRequest);
                searchReq.SearchFunc = x => x.Name.ToLower().Contains(searchText) || x.Description.ToLower().Contains(searchText);
                sourceResponce = await _repo.GetAllByRequestAsync(searchReq);
            }
            else
            {
                //_webLogMgr.Log($"[CommodityItemFrontendManager]: not using search");
                sourceResponce = await _repo.GetAllByRequestAsync(repositoryRequest);
            }
            
            var str = JsonConvert.SerializeObject(sourceResponce);
          //  _webLogMgr.Log($"[CommodityItemFrontendManager]: responce is {str}");

            var targetResponce = _mapper.ResponceFrontFromResponceCommItem(sourceResponce);

            foreach (var x in targetResponce.Items)
            {
                x.Pictures = GetPictureInfoListForItem(x);
            }

            targetResponce.TotalCount = await _repo.GetCountAsync();

            return targetResponce;
        }


        public async Task<int> Count()
        {
            return await _repo.GetCountAsync();
        }
        public async Task<CommodityItemFrontend> GetByIdOrNull(Guid id)
        {
            var item = _mapper.CommodityItemFrontendFromCommodityItem(await _repo.GetByIdOrNullAsync(id));
            item.Pictures = GetPictureInfoListForItem(item);
            item.Supplier = await _supplierRepo.GetByIdOrNullAsync(item.SupplierId);
            return item;
        }

        public async Task<List<CommodityItemFrontend>> Search(string text)
        {
            var rez = await _repo.SearchAsync(x => x.Name.ToLower().Contains(text) || (string.IsNullOrEmpty(x.Description) ? false : x.Description.ToLower().Contains(text)) );
            var rezFront = rez.Select(x => _mapper.CommodityItemFrontendFromCommodityItem(x)).ToList();
            return rezFront;
        }
        private List<PictureInfo> GetPictureInfoListForItem(CommodityItemFrontend item)
        {
            var picLst = new List<PictureInfo>();

            for (int i = 1; i <= 3; i++)
            {
                var pic = new PictureInfo();
                pic.BigPictureFullPath = $"{_app.BaseUrl}/CommodityItemImages/{item.Id}/{i}.jpg";
                pic.MediumPictureFullPath = $"{_app.BaseUrl}/CommodityItemImages/{item.Id}/{i}m.jpg";
                pic.SmallPictureFullPath = $"{_app.BaseUrl}/CommodityItemImages/{item.Id}/{i}s.jpg";
                picLst.Add(pic);
            }
            return picLst;
        }
    }
}
