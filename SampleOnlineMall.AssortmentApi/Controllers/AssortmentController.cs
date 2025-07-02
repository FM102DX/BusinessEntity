using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Service;
using SampleOnlineMall.WebLogger.Services;
using System;
using System.Collections.Generic;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.Core.Mappers;

namespace SampleOnlineMall
{
    [ApiController]
    [Route("")]
    public class AssortmentController : Controller
    {
        public CommodityItemManager _commodityItemManager { get; set; }
        public IWebLoggerService _logger { get; set; }
        
        private IAsyncRepository<CommodityItemApiFeed> _repo;
        private CustomMapper _mapper;



        public AssortmentController(CommodityItemManager commodityItemManager, 
                                    IWebLoggerService logger,
                                    CustomMapper mapper)
        {
            _logger = logger;
            _commodityItemManager= commodityItemManager;
            _logger.Information("AssortmentController initialized");
            _logger.SetActiveStatus(true);
            _mapper = mapper;
        }

        [HttpGet]
        [Route("info/")]
        public async Task<IActionResult> Info()
        {
            string reply;
            try
            {
                reply = "This is assortment controller ping";
                _logger.Information(reply);
                Console.WriteLine(reply);
                return StatusCode(200, reply);
            }
            catch (Exception ex)
            {
                reply = $"An error occurred: {ex.Message} {ex.InnerException?.Message}";
                _logger.Information(reply);
                Console.WriteLine(reply);
                return StatusCode(500, reply);
            }
        }

        [HttpGet]
        [Route("getall/")]
        public async Task<IActionResult> GetAllItems()
        {
            string reply;
            try
            {
                var itemsTmp = await _commodityItemManager.GetAll();
                var items=itemsTmp.ToList().Select(x => _mapper.CommodityItemToCommodityItemApiFeed(x));
                
                reply = $"[AssortApi] giving away {items.Count()} items with picpath={items.ToList()[0].FirstPic}";
                Console.WriteLine(reply);
                await _logger.Information(reply);
                return Ok(items); // Возвращаем статус 200 и список items
            }
            catch (Exception ex)
            {
                reply = $"An error occurred: {ex.Message} {ex.InnerException?.Message}";
                Console.WriteLine(reply);
                return StatusCode(500, reply);
            }
        }

        [HttpDelete]
        [Route("clear/")]
        public async Task<IActionResult> DeleteAllCommodityItems()
        {
            var rezult = await _commodityItemManager.DeleteAll();
            if (rezult.Success)
            {
                return StatusCode(200, CommonOperationResult.SayOk());
            }
            else
            {
                return StatusCode(500, CommonOperationResult.SayFail(rezult.Message));
            }
        }

        [HttpPost]
        [Route("insertitem/")]
        public async Task<IActionResult> InsertCommodityItem([FromBody] CommodityItemApiFeed commodityItem)
        {
            _logger.Information($"[ASSORT][CTRL][INS] Entering assort api INSERT method, gonna insert object {commodityItem.Name}");
            _logger.SendObject(commodityItem);
            var rezult = await _commodityItemManager.InsertFromWebApi(commodityItem);

            if (rezult.Success)
            {
                _logger.Information($"[ASSORT][CTRL][INS] Successfully added assortment item name={commodityItem.Name} msg={rezult.Message}");
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                _logger.Error($"[ASSORT][CTRL][INS][ERR] Error while adding assort position name={commodityItem.Name} err={rezult.Message}");
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
