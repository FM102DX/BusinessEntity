using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Макро-описание допустимой связи между типами сущностей
namespace BusinessEntity.Core.Classes
{
    // Используется как шаблон для создания relation
    public class MacroRelationType
    {
        // Условное имя правила связи
        public string RelationName { get; set; } = string.Empty;
        // Тип объекта слева
        public BusinessEntityTypeEnum RelationObjectTypeA { get; set; }
        // Тип объекта справа
        public BusinessEntityTypeEnum RelationObjectTypeB { get; set; }
        // Конкретный тип relation
        public BusinessEntityRelationTypeEnum RelationType { get; set; }

        // Пустой конструктор для инициализации объекта
        public MacroRelationType()
        {
        }

        // Быстрое создание макро-связи с заполнением полей
        public MacroRelationType(string relationName, BusinessEntityTypeEnum typeA, BusinessEntityTypeEnum typeB, BusinessEntityRelationTypeEnum relationType)
        {
            RelationName = relationName;
            RelationObjectTypeA = typeA;
            RelationObjectTypeB = typeB;
            RelationType = relationType;
        }
    }
} 
