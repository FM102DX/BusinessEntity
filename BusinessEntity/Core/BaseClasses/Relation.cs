using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public class Relation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ObjectAId { get; set; }
        public Guid ObjectBId { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public string RelationParams { get; set; } = string.Empty;
    }
} 