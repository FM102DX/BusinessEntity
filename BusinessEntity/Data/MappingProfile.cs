using AutoMapper;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Models;
namespace BusinessEntity.Data
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
