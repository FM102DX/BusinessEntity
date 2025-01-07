using AutoMapper;
using SampleOnlineMall.WebLogger.Models;
namespace BlazorServerWebLogger.Data
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Добавьте маппинг между DTO и сущностью базы данных
            CreateMap<LogEntryTransferDto, LogEntryDbStorable>();
        }
    }
}
