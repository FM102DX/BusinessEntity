using BusinessEntity.Core.BaseClasses.Relations;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Поставщик допустимых правил связей Contains
namespace BusinessEntity.Core.Services
{
    // Хранит преднастроенные macro relation между типами entity
    public class PossibleEntityRelationTypesProvider : IPossibleEntityRelationTypesProvider
    {
        // Внутренний список разрешенных связей
        private readonly List<MacroRelationType> _possibleRelations;

        // Инициализирует стандартный набор разрешенных связей
        public PossibleEntityRelationTypesProvider()
        {
            // Формируем стартовый список правил для дерева
            _possibleRelations = new List<MacroRelationType>
            {
                // Базовые правила дерева строятся на связи Contains
                new MacroRelationType(
                    relationName: "basic:space-contains-folder",
                    typeA: BusinessEntityTypeEnum.Space,
                    typeB: BusinessEntityTypeEnum.Folder,
                    relationType: BusinessEntityRelationTypeEnum.Contains
                ),
                // Space может содержать Document напрямую
                new MacroRelationType(
                    relationName: "basic:space-contains-page",
                    typeA: BusinessEntityTypeEnum.Space,
                    typeB: BusinessEntityTypeEnum.Document,
                    relationType: BusinessEntityRelationTypeEnum.Contains
                ),
                // Тестовый объект: Папка содержит страницу
                new MacroRelationType(
                    relationName: "basic:folder-contains-page",
                    typeA: BusinessEntityTypeEnum.Folder,
                    typeB: BusinessEntityTypeEnum.Document,
                    relationType: BusinessEntityRelationTypeEnum.Contains
                ),
                new MacroRelationType(
                    relationName: "basic:folder-contains-folder",
                    typeA: BusinessEntityTypeEnum.Folder,
                    typeB: BusinessEntityTypeEnum.Folder,
                    relationType: BusinessEntityRelationTypeEnum.Contains
                )
            };
        }

        // Возвращает список допустимых макро-связей
        public IEnumerable<MacroRelationType> GetPossibleRelations()
        {
            return _possibleRelations.AsReadOnly();
        }
    }
} 
