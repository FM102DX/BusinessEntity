using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Набор допустимых типов связей между сущностями
namespace BusinessEntity.Core.Classes
{
    // Используется в relation-слое и в макро-описании связей
    public enum BusinessEntityRelationTypeEnum
    {
        // Физическое хранение в папке
        StoredInFolder = 100,
        // Произвольная смысловая связь
        RelatesTo = 200,
        // Структурное включение в состав
        IsStructuralPartOf = 300,

        // Базовое содержимое одного объекта другим
        Contains = 1000,
        
        // Визуальное содержание в дереве UI
        VisuallyContains = 1100,

        // Неопределенный тип связи
        Undefined = 9999
    }
}
