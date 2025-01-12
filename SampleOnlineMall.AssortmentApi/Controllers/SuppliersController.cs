using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.Service;
using SampleOnlineMall.WebLogger.Services;
using System;
using System.Collections.Generic;



namespace SampleOnlineMall
{
    [ApiController]
    [Route("Suppliers/")]
    public class Suppliers : Controller
    {
        public IWebLoggerService _logger { get; set; }
        public SupplierManager _manager { get; set; }
        public Suppliers(SupplierManager manager, IWebLoggerService logger)
        {
            _logger = logger;
            _manager = manager;
        }

        [HttpGet]
        public async Task<string> Info()
        {
            int cnt = await _manager.Count();
            return $"This is suppliers controller. Now {cnt} suppliers in database";
        }

        [HttpGet]
        [Route("GetByIdOrNull/{id}")]
        public async Task<Supplier> GetByIdOrNull(Guid id)
        {
            return await _manager.GetByIdOrNull(id);
        }

        [HttpDelete]
        [Route("deleteallitems/")]
        public async Task<IActionResult> DeleteAllCommodityItems()
        {
            var rezult = await _manager.DeleteAll();
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
        public async Task<IActionResult> InsertCommodityItem([FromBody] Supplier item)
        {
            var rezult = await _manager.InsertFromWebApi(item);

            if (rezult.Success)
            {
                _logger.Information($"Successfully added supplier name={item.Name} msg={rezult.Message}");
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                _logger.Error($"Error while adding supplier name={item.Name} err={rezult.Message}");
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
