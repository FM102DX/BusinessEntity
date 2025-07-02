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

namespace SampleOnlineMall.Core.Managers
{
    public class WebLoggerSettingsManager
    {
        private IAsyncRepository<WebLoggerSettings> _repo;
        private Serilog.ILogger _logger;
        private SampleOnlineMallWebLoggerApp _app;
        public WebLoggerSettingsManager(IAsyncRepository<WebLoggerSettings> repo, Serilog.ILogger logger, SampleOnlineMallWebLoggerApp app)
        {
            _repo = repo;
            _logger = logger;
            _app = app;
        }

        public async Task<IEnumerable<WebLoggerSettings>> GetAll()
        {
            try
            {
                var items = (await _repo.GetAllAsync()).ToList();
                if (items.Count == 0)
                {
                    var settings = new WebLoggerSettings() { LogsKeepingPeriod = TimeSpan.Parse("23:59:59"), RefreshPeriodMs = 3000, ItemsPerPage = 50 };
                    var saveRez = await _repo.AddAsync(settings);
                    if (!saveRez.Success)
                    {
                        throw new ApplicationException(saveRez.Message);
                    }

                    return new List<WebLoggerSettings>() { settings };
                }
                else
                {
                    return items;
                }
            }
            catch (Exception ex) 
            {
                _logger.Error($"[WebLoggerSettingsManager.GetAll] ex={ex.Message} innerEx={ex.InnerException}");
            }

            var rez = new List<WebLoggerSettings>();
            var rezIe = (IEnumerable<WebLoggerSettings>)rez;
            return rezIe;
        }
          
        public async Task<CommonOperationResult> Save (WebLoggerSettings item)
        {
            try
            {
                var saveRez = await _repo.UpdateAsync(item);

                if (!saveRez.Success)
                {
                    return CommonOperationResult.SayFail(saveRez.Message);
                }
                return CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                _logger.Error($"[WebLoggerSettingsManager.Save] {ex.Message}");

                return CommonOperationResult.SayFail($"Ex={ex.Message} InnerEx={ex.InnerException}");
            }
        }
    }
}
