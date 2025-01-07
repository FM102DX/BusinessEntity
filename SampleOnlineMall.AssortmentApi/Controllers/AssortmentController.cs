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
    public class AssortmentController : Controller
    {
        public Serilog.ILogger _logger { get; set; }
        public CommodityItemManager _commodityItemManager { get; set; }
        public IWebLoggerService _wLogger { get; set; }

        public AssortmentController(CommodityItemManager commodityItemManager, Serilog.ILogger logger, IWebLoggerService wLogger)
        {
            _logger = logger;
            _commodityItemManager= commodityItemManager;
            _wLogger= wLogger;
            _wLogger.SetActiveStatus(true);
        }

        [HttpDelete]
        [Route("deleteallitems/")]
        public async Task<IActionResult> DeleteAllCommodityItems()
        {
            var rezult = await _commodityItemManager.DeleteAll();
            if (rezult.Success)
            {
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }
        }

        [HttpPost]
        [Route("insertitem/")]
        public async Task<IActionResult> InsertCommodityItem([FromBody] CommodityItemApiFeed commodityItem)
        {
            var rezult = await _commodityItemManager.InsertFromWebApi(commodityItem);

            if (rezult.Success)
            {
                _wLogger.Information($"Successfully added assortment item name={commodityItem.Name} msg={rezult.Message}");
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                _wLogger.Error($"Error while adding assort position name={commodityItem.Name} err={rezult.Message}");
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
