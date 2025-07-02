using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.DataAccess.Models;
using System;
using SampleOnlineMall.WebLogger.Services;


namespace SampleOnlineMall.FrontEnd.BlazorServer.Data
{
    public class StoreManager
    {
        private IAsyncRepository<CommodityItemFrontend> _repo { get; set; }

        private SampleOnlineMallFrontEndBlazorApp _app { get; set; }
        
        private CustomMapper _mapper { get; set; }

        public string StoreBaseUrl { get; set; }
        private IWebLoggerService _webLogger;

        public StoreManager(NavigationManager Navi, 
                IAsyncRepository<CommodityItemFrontend> repo, 
                SampleOnlineMallFrontEndBlazorApp app, 
                CustomMapper mapper,
                IWebLoggerService webLogger)
        {
            StoreBaseUrl = Navi.BaseUri;
            _repo = repo;
            _app = app;
            _mapper = mapper;
            _webLogger = webLogger;
        }
        public string GetItemPageFullAddress(Guid itemId)
        {
            return $"{StoreBaseUrl}Item/" + itemId.ToString();
        }
        public async Task<List<CommodityItemFrontendDisplayed>> GetAllAsync()
        {
            return (await _repo.GetAllAsync()).Select(x => _mapper.CommodityItemFrontendDisplayedFromTransport(x)).ToList();
        }
        public async Task<RepositoryResponce<CommodityItemFrontendDisplayed>> GetAllAsync(RepositoryRequestTextSearch repositoryRequest)
        {
            var resp = await _repo.GetAllByRequestAsync(repositoryRequest);
            return _mapper.ResponceFrontDisplayedFromTransport(resp);
        }

        public async Task<List<CommodityItemFrontendDisplayed>> Search(string searchText)
        {
            return (await _repo.SearchAsync(searchText)).Select(x=>_mapper.CommodityItemFrontendDisplayedFromTransport(x)).ToList();
        }

        public async Task<CommodityItemFrontendDisplayed> GetByIdOrNullAsync(Guid id)
        {
            return _mapper.CommodityItemFrontendDisplayedFromTransport(await _repo.GetByIdOrNullAsync(id));
        }


        



    }
}
