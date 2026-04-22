using BusinessEntity.Core.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Контракт источника допустимых связей между типами сущностей
namespace BusinessEntity.Core.Contracts
{
    // Возвращает набор правил для UI и бизнес-логики связей
    public interface IPossibleEntityRelationTypesProvider
    {
        // Отдает все зарегистрированные варианты связей
        IEnumerable<MacroRelationType> GetPossibleRelations();
    }
} 
