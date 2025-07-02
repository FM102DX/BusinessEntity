using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Service;
using System;
using System.Collections.Generic;



namespace SampleOnlineMall.WebLogger
{
    [ApiController]
    [Route("")]
    public class WebLoggerController : Controller
    {
        public Serilog.ILogger _logger { get; set; }
        public WebLoggerMessageManager _itemManager { get; set; }

        public WebLoggerController(WebLoggerMessageManager commodityItemManager, Serilog.ILogger logger)
        {
            _logger = logger;
            _itemManager= commodityItemManager;
        }
        [HttpGet]
        [Route("getall/")]
        public async Task<IEnumerable<WebLoggerMessage>> GetAllMessages()
        {
            return await _itemManager.GetAll();
        }

        [HttpGet]
        [Route("getallbysender/")]
        public async Task<IEnumerable<WebLoggerMessage>> GetAllMessagesBySender(string sender)
        {
            return await _itemManager.GetAllBySender();
        }

        [HttpDelete]
        [Route("deleteallitems/")]
        public async Task<IActionResult> DeleteAllCommodityItems()
        {
            var rezult = await _itemManager.DeleteAll();
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
        public async Task<IActionResult> InsertCommodityItem([FromBody] WebLoggerMessage item)
        {
            var rezult = await _itemManager.InsertFromWebApi(item);

            if (rezult.Success)
            {
                return StatusCode(201, CommonOperationResult.SayOk());
            }
            else
            {
                return StatusCode(501, CommonOperationResult.SayFail(rezult.Message));
            }

        }
    }
}
