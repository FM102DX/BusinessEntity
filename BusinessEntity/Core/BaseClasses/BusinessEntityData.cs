using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessEntity.Core.Contracts;

// Базовый heavy-data объект для типизированных payload
namespace BusinessEntity.Core.Classes
{
    // Общая база для Document и других тяжелых бизнес-объектов
    public class BusinessEntityData : IBusinessEntityData
    {
        // Идентификатор data-объекта
        public Guid Id { get; set; } = Guid.NewGuid();
        // Дата создания data-объекта
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        // Дата последнего изменения data-объекта
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        // Локальный пользователь, создавший связанный бизнес-объект
        public Guid? CreatedByUserId { get; set; }
        // Локальный пользователь, последним изменивший связанный бизнес-объект
        public Guid? LastModifiedByUserId { get; set; }

        // Имя, синхронизируемое с родительской entity
        public string Name { get; set; } = string.Empty;
        // Тип data-объекта
        public virtual BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
        // Дополнительная строковая метка
        public string Tag { get; set; } = string.Empty;
        // Номер версии payload-объекта в storage.
        public int Version { get; set; } = 1;
        // По умолчанию payload не версионируется.
        public virtual bool HasVersions => false;
        // По умолчанию payload хранится без чанков.
        public virtual BusinessEntityDataChunkStorageType ChunkStorageType => BusinessEntityDataChunkStorageType.None;

    }
}
