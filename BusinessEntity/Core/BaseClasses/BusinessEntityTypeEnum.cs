using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/// Внимание! 
/// Номера типов не менять. Они сохраняются в БД как integer. 
/// Любое изменение чисел сломает соответствие данных.
/// 
/// Набор базовых типов бизнес-сущностей
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
        // Rich-text документ с чанковым хранением и embedded-файлами
        RichTextDocument = 350,
        // Системные параметры приложения
        SysParametersTp = 400,
        // Видео в общем мультимедиа-хранилище
        MediaVideo = 500,

        // Неопределенный тип
        Undefined = 9999
    }
}
