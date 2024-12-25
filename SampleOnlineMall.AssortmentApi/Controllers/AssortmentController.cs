using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Service;
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
        public WebLoggerManager _webLoggerManager { get; set; }

        public AssortmentController(CommodityItemManager commodityItemManager, WebLoggerManager webLoggerManager, Serilog.ILogger logger)
        {
            _logger = logger;
            _commodityItemManager= commodityItemManager;
            _webLoggerManager= webLoggerManager;
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
                _webLoggerManager.Information($"Successfully added assortment item name={commodityItem.Name} msg={rezult.Message}");
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                _webLoggerManager.Error($"Error while adding assort position name={commodityItem.Name} err={rezult.Message}");
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
