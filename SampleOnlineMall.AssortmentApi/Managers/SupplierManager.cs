using SampleOnlineMall.Core.Appilcation;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using Shim= SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Formats;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.Core.Models;

namespace SampleOnlineMall.Core.Managers
{
    public class SupplierManager
    {
        private IAsyncRepository<Supplier> _repo;
        private Serilog.ILogger _logger;
        private SampleOnlineMallAssortmentApiApp _app;
        private WebLoggerManager _webLogger;
        public SupplierManager(IAsyncRepository<Supplier> repo, Serilog.ILogger logger, SampleOnlineMallAssortmentApiApp app)
        {
            _repo = repo;
            _logger = logger;
            _app = app;

        }

        public async Task<IEnumerable<Supplier>> GetAll()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<Supplier> GetByIdOrNull(Guid id)
        {
            return await _repo.GetByIdOrNullAsync(id);
        }

        public async Task<int> Count()
        {
            return await _repo.GetCountAsync();
        }

        public async Task<CommonOperationResult> DeleteItemById(Guid id)
        {
            try
            {
                await _repo.DeleteAsync(id);
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"{ex.Message}");
            }
        }

        public async Task<CommonOperationResult> DeleteAll()
        {
            try
            {
                var itemList = await _repo.GetAllAsync();
                var itemGuidArray = itemList.ToList().Select(x => x.Id).ToArray();
                foreach(var id in itemGuidArray)
                {
                    await DeleteItemById(id);
                }
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"{ex.Message}");
            }
        }

        public async Task<CommonOperationResult> InsertFromWebApi (Supplier item)
        {
            _logger.Information($"This is Supplier manager. Received commodity item name={item.Name}");
            _webLogger.Log($"inserting item name={item.Name}");
            try
            {
                var exists = await _repo.Exists(item.Id);
                _logger.Information($"Checking item existance name={item.Name}, result: {exists}");
                if (exists)
                {
                    return CommonOperationResult.SayFail($"Unable to insert assortment item with name={item.Name}");
                }
                //saving object
                var saveRez = await _repo.AddAsync(item);
                if(!saveRez.Success) return CommonOperationResult.SayFail(saveRez.Message);
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.Message}");
                return CommonOperationResult.SayFail($"Ex={ex.Message} InnerEx={ex.InnerException}");
            }
        }
    }
}
