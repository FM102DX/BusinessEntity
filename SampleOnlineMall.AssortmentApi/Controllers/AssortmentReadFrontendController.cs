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
    [Route("frontend")]
    public class AssortmentReadFrontendController : Controller
    {
        public IWebLoggerService _logger { get; set; }
        public CommodityItemFrontendManager _itemManager { get; set; }
        public AssortmentReadFrontendController(CommodityItemFrontendManager itemManager, IWebLoggerService logger)
        {
            _logger = logger;
            _itemManager= itemManager;
        }


        [HttpGet]
        [Route("getall/")]
        public async Task<IActionResult> GetAllItems()
        {
            string reply;
            try
            {
                
                var items = await _itemManager.GetAll();
                reply = $"[AssortApi] giving away {items.Count()} items";
                Console.WriteLine(reply);
                await _logger.Information(reply);
                return Ok(items); // Возвращаем статус 200 и список items
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message} {ex.InnerException?.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpPost]
        [Route("getallbyrequest/")]
        public async Task<RepositoryResponce<CommodityItemFrontend>> GetAllByRequest(RepositoryRequestTextSearch request)
        {
            var str = JsonConvert.SerializeObject(request);
            _logger.Information($"Controller: got request {str}");
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
