using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Service.WebLogging;
using BlazorServerWebLogger.Services;

namespace BlazorServerWebLogger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebLoggerController : ControllerBase
    {
        private readonly ILogIngestionQueue _logIngestionQueue;

        public WebLoggerController(ILogIngestionQueue logIngestionQueue)
        {
            _logIngestionQueue = logIngestionQueue ?? throw new ArgumentNullException(nameof(logIngestionQueue));
        }

        /// <summary>
        /// Метод для создания записи логов.
        /// </summary>
        /// <param name="logEntryDto">DTO лог-записи</param>
        /// <returns>Результат операции</returns>
        [HttpPost("CreateLogRecord")]
        public async Task<IActionResult> CreateLogRecord([FromBody] LogEntryTransferDto logEntryDto)
        {
            try
            {
                await _logIngestionQueue.EnqueueAsync(logEntryDto, HttpContext.RequestAborted);
                return Ok("Log record queued successfully.");
            }
            catch (Exception ex)
            {
                // Обработка ошибок
                return StatusCode(500, $"An error occurred: err={ex.Message} inn={ex.InnerException}");
            }
        }

        /// <summary>
        /// Метод для проверки доступности API.
        /// </summary>
        /// <returns>Сообщение о доступности API</returns>
        [HttpGet("Info")]
        public IActionResult Info()
        {
            return Ok("Hi, this is web-logger!");
        }
    }
}
