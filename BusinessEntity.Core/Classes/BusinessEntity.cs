using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntity : BusinessEntityBase, IBusinessEntity
    {
        public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
        
        /// <summary>
        /// Переопределяем свойство для возможности переименования в зависимости от типа сущности
        /// </summary>
        public override bool IsRenameableInVisualTree 
        { 
            get 
            { 
                // Папки можно переименовывать
                return EntityType == BusinessEntityTypeEnum.Folder; 
            } 
            set 
            { 
                // Для совместимости с базовым классом, но значение определяется типом сущности
                base.IsRenameableInVisualTree = value; 
            } 
        }
    }
}