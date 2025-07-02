using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SampleOnlineMall.WebLogger.Models;
using BlazorServerWebLogger.Contracts;
using AutoMapper;
using SampleOnlineMall.Service.WebLogging;

namespace BlazorServerWebLogger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebLoggerController : ControllerBase
    {
        private readonly IRepositoryFactory<LogEntryDbStorable> _repositoryFactory;
        private readonly IMapper _mapper;

        public WebLoggerController(IRepositoryFactory<LogEntryDbStorable> repositoryFactory, IMapper mapper)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
                var repository = _repositoryFactory.GetRepository();

                // Маппинг DTO в сущность для базы данных
                var logEntryDb = _mapper.Map<LogEntryDbStorable>(logEntryDto);
                logEntryDb.Id=Guid.NewGuid();
                // Сохранение в базе данных
                await repository.InsertAsync(logEntryDb);

                return Ok("Log record created successfully.");
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
