using System;
using System.Threading.Tasks;
using BlazorServerWebLogger.Contracts;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Data.Services.HostedServices
{
    public class LogWriterService
    {
        private readonly IAsyncRepository<LogEntryDbStorable> _logRepository;

        public LogWriterService(IAsyncRepository<LogEntryDbStorable> logRepository)
        {
            _logRepository = logRepository ?? throw new ArgumentNullException(nameof(logRepository));
        }

        public async Task WriteToLogAsync(string message, string messageType)
        {
            var logEntry = new LogEntryDbStorable
            {
                Id = Guid.NewGuid(),
                ServiceCode = "self",
                MessageType = messageType,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            var result = await _logRepository.InsertAsync(logEntry);
        }
    }
}