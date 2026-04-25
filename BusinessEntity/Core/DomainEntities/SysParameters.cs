using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;

// Typed data-объект системных параметров
namespace BusinessEntity.Core.DomainEntities
{
    // Хранит общие настройки системы без связей в дереве
    public class SysParameters : BusinessEntityData, IBusinessEntityData
    {
        // Тип business-объекта системных параметров
        public override BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.SysParametersTp;
        // Название компании для отображения в системе
        public string CompanyName { get; set; } = string.Empty;
    }
}
