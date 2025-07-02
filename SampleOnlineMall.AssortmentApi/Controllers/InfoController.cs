using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Service;
using SampleOnlineMall.WebLogger.Services;
using System;
using System.Collections.Generic;



namespace SampleOnlineMall
{
    [ApiController]
    [Route("")]
    public class InfoController : Controller
    {
        public IWebLoggerService _logger { get; set; }
        public CommodityItemManager _commodityItemManager { get; set; }

        public InfoController(CommodityItemManager commodityItemManager, IWebLoggerService logger)
        {
            
            _logger = logger;
            _logger.Information("InfoController_ctor_0");
            _commodityItemManager = commodityItemManager;
        }

        [HttpGet]
        public async Task<string> Info()
        {
            var s = "";
            try
            {
                int cnt = await _commodityItemManager.Count();
                var positions = string.Join("<br />", _commodityItemManager.GetAll().Result.ToList().Select(x => $"{x.Name}"));
                return $"This is AssortmentController. Now {cnt} positions in assortment {positions}";
                
            }
            catch(Exception ex)
            {
                _logger.Information("InfoController_Info_4_ERRROR");
                _logger.Information($"{ex.Message}");
                _logger.Information($"{ex.InnerException}");
            }
            return string.Empty;
        }
    }
}
