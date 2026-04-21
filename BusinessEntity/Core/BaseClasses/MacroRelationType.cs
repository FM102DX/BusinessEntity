using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public class MacroRelationType
    {
        public string RelationName { get; set; } = string.Empty;
        public BusinessEntityTypeEnum RelationObjectTypeA { get; set; }
        public BusinessEntityTypeEnum RelationObjectTypeB { get; set; }
        public BusinessEntityRelationTypeEnum RelationType { get; set; }

        public MacroRelationType()
        {
        }

        public MacroRelationType(string relationName, BusinessEntityTypeEnum typeA, BusinessEntityTypeEnum typeB, BusinessEntityRelationTypeEnum relationType)
        {
            RelationName = relationName;
            RelationObjectTypeA = typeA;
            RelationObjectTypeB = typeB;
            RelationType = relationType;
        }
    }
} 