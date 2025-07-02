using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Service;
using System;
using System.Collections.Generic;



namespace SampleOnlineMall.WebLogger
{
    [ApiController]
    [Route("settings")]
    public class WebLoggerSettingsController : Controller
    {
        public Serilog.ILogger _logger { get; set; }
        public WebLoggerSettingsManager _itemManager { get; set; }

        public WebLoggerSettingsController(WebLoggerSettingsManager itemManager, Serilog.ILogger logger)
        {
            _logger = logger;
            _itemManager = itemManager;
        }

        [HttpGet]
        [Route("getall/")]
        public async Task<IEnumerable<WebLoggerSettings>> GetAllMessages()
        {
            return await _itemManager.GetAll();
        }

        [HttpPut]
        [Route("save/")]
        public async Task<IActionResult> Save([FromBody] WebLoggerSettings item)
        {
            var rezult = await _itemManager.Save(item);

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
