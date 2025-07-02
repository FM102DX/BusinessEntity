using Newtonsoft.Json;
using SampleOnlineMall.Core;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.Service;
using Serilog;

namespace WebLogger.Blazor.Core.Managers
{
    public class WebLoggerSettingsManagerClient
    {

        Serilog.ILogger _logger;
        IAsyncRepository<WebLoggerSettings> _repo;

        public WebLoggerSettingsManagerClient(Serilog.ILogger logger, IAsyncRepository<WebLoggerSettings> repo)
        {
            _logger = logger;
            _repo = repo;
        }

        public async Task<WebLoggerSettings> GetOne()
        {
            var rez = (await _repo.GetAllAsync()).FirstOrDefault();
            return rez;
        }

        public async Task<string> GetAsJson()
        {
            var rez = (await GetOne());
            var rezJson = JsonConvert.SerializeObject(rez);
            return rezJson;
        }

        public async Task<CommonOperationResult> Save(WebLoggerSettings item)
        {
            var rez = await _repo.UpdateAsync(item);
            return rez;
        }



    }
}
