using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Services
{
    public class PossibleEntityRelationTypesProvider : IPossibleEntityRelationTypesProvider
    {
        private readonly List<MacroRelationType> _possibleRelations;

        public PossibleEntityRelationTypesProvider()
        {
            _possibleRelations = new List<MacroRelationType>
            {
                // Дополнительные примеры отношений
                new MacroRelationType(
                    relationName: "basic:space-contains-folder",
                    typeA: BusinessEntityTypeEnum.Space,
                    typeB: BusinessEntityTypeEnum.Folder,
                    relationType: BusinessEntityRelationTypeEnum.Contains
                ),
                // Тестовый объект: Папка содержит страницу
                new MacroRelationType(
                    relationName: "basic:folder-contains-page",
                    typeA: BusinessEntityTypeEnum.Folder,
                    typeB: BusinessEntityTypeEnum.Page,
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

        public IEnumerable<MacroRelationType> GetPossibleRelations()
        {
            return _possibleRelations.AsReadOnly();
        }
    }
} 