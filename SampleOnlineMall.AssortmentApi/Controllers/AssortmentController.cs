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
        public CommodityItemManager _commodityItemManager { get; set; }
        public IWebLoggerService _logger { get; set; }

        public AssortmentController(CommodityItemManager commodityItemManager, 
                                    IWebLoggerService logger)
        {
            _logger = logger;
            _commodityItemManager= commodityItemManager;
            _logger.SetActiveStatus(true);
            _logger.Information("AssortmentController initialized");
        }

        [HttpGet]
        [Route("info/")]
        public async Task<string> Info()
        {
            try
            {
                _logger.Information($"This is assortment controller ping");
                Console.WriteLine($"This is assortment controller ping");
                return $"This is assortment controller ping";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return $"An error occurred: {ex.Message}";
            }
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
            _logger.Information($"_entering assort api method");
            var rezult = await _commodityItemManager.InsertFromWebApi(commodityItem);

            if (rezult.Success)
            {
                _logger.Information($"Successfully added assortment item name={commodityItem.Name} msg={rezult.Message}");
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                _logger.Error($"Error while adding assort position name={commodityItem.Name} err={rezult.Message}");
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
