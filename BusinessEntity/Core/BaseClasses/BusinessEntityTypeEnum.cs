using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Набор базовых типов бизнес-сущностей
namespace BusinessEntity.Core.Classes
{
    // Используется и для entity, и для data-объектов
    public enum BusinessEntityTypeEnum
    {
        // Пространство верхнего уровня
        Space=100,
        // Папка в дереве
        Folder = 200,
        // Текстовый документ
        Document = 300,

        // Неопределенный тип
        Undefined = 9999
    }
}
