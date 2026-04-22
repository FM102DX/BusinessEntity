using System;

// Базовая связь между двумя бизнес-сущностями
namespace BusinessEntity.Core.Classes
{
    // Хранит пару связанных объектов и тип связи между ними
    public class BusinessEntityRelation
    {
        // Идентификатор связи
        public Guid Id { get; set; } = Guid.NewGuid();
        // Дата создания связи
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        // Дата последнего изменения связи
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        // Левая сторона связи
        public Guid ObjectAId { get; set; }
        // Правая сторона связи
        public Guid ObjectBId { get; set; }
        // Имя типа связи
        public string RelationType { get; set; } = string.Empty;
        // Дополнительные параметры связи
        public string RelationParams { get; set; } = string.Empty;
    }
}
