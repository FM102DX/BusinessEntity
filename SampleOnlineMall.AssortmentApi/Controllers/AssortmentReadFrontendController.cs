using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SampleOnlineMall.WebLogger.Services;



namespace SampleOnlineMall
{
    [ApiController]
    [Route("")]
    public class AssortmentReadFrontend : Controller
    {
        public Serilog.ILogger _logger { get; set; }
        public CommodityItemFrontendManager _itemManager { get; set; }
        public IWebLoggerService _wLogger { get; set; }
        public AssortmentReadFrontend(CommodityItemFrontendManager itemManager, IWebLoggerService wLogger, Serilog.ILogger logger)
        {
            _logger = logger;
            _itemManager= itemManager;
            _wLogger = wLogger;
        }

        [HttpGet]
        [Route("getall/")]
        public async Task<IEnumerable<CommodityItemFrontend>> GetAllItems()
        {
            return await _itemManager.GetAll();
        }

        [HttpPost]
        [Route("getallbyrequest/")]
        public async Task<RepositoryResponce<CommodityItemFrontend>> GetAllByRequest(RepositoryRequestTextSearch request)
        {
            var str = JsonConvert.SerializeObject(request);
            _wLogger.Information($"Controller: got request {str}");
            return await _itemManager.GetAllByRequest(request);
        }

        [HttpGet]
        [Route("search/{searchText}")]
        public async Task<IEnumerable<CommodityItemFrontend>> SearchFrontednAssort(string searchText)
        {
            return await _itemManager.Search(searchText.ToLower());
        }

        [HttpGet]
        [Route("GetByIdOrNull/{id}")]
        public async Task<CommodityItemFrontend> GetByIdOrNull(Guid id)
        {
            return await _itemManager.GetByIdOrNull(id);
        }
    }
}
