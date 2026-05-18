using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Typed data-объект документа
namespace BusinessEntity.Core.DomainEntities
{
    // Хранит текст документа и общие поля data-объекта
    public class Document : BusinessEntityData, IBusinessEntityData
    {
        // Документ всегда имеет тип Document
        public override BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Document;
        // Обычный документ сохраняет историю payload при каждом сохранении.
        public override bool HasVersions => true;
        // Номер версии документа, которая считается опубликованной. 0 означает, что публикация не задана.
        public int PublishedVersion { get; set; }
        // Основной текст документа
        public string Text { get; set; } = string.Empty;
    }
}
