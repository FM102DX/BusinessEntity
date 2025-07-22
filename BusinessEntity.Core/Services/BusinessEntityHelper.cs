using System;
using System.Threading.Tasks;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System.Linq;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Core.Services
{
    public class BusinessEntityHelper
    {
        private readonly IAsyncRepository<Classes.BusinessEntity> _businessEntityRepository;
        private readonly IAsyncRepository<Relation> _relationRepository;
        private readonly IWebLoggerService? _webLogger;

        public BusinessEntityHelper(
            IAsyncRepository<Classes.BusinessEntity> businessEntityRepository, 
            IAsyncRepository<Relation> relationRepository,
            IWebLoggerService? webLogger)
        {
            _businessEntityRepository = businessEntityRepository ?? throw new ArgumentNullException(nameof(businessEntityRepository));
            _relationRepository = relationRepository ?? throw new ArgumentNullException(nameof(relationRepository));
            _webLogger = webLogger; // Логгер может быть не настроен, поэтому допускаем null
        }

        public async Task<Classes.BusinessEntity> CreateBusinessEntity(BusinessEntityTypeEnum type, string name)
        {
            _webLogger?.Information($"CreateBusinessEntity: type={type}, name={name}");
            var entity = new Classes.BusinessEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                EntityType = type,
                BusinessEntityType = type
            };

            return await _businessEntityRepository.AddAsync(entity);
        }

        public async Task RemoveBusinessEntity(Guid id)
        {
            _webLogger?.Warning($"RemoveBusinessEntity: id={id}");
            // Сначала удаляем все связи, где участвует данная сущность
            var relations = await _relationRepository.GetAllAsync(r => r.ObjectAId == id || r.ObjectBId == id);
            
            foreach (var relation in relations)
            {
                await _relationRepository.DeleteAsync(relation.Id);
            }

            // Затем удаляем саму сущность
            await _businessEntityRepository.DeleteAsync(id);
        }

        public async Task<Relation> CreateRelation(IBusinessEntity entityA, IBusinessEntity entityB, MacroRelationType macroRelationType, string parameters = "")
        {
            _webLogger?.Information($"CreateRelation: {entityA?.Name} -> {entityB?.Name} relation={macroRelationType?.RelationType}");
            if (entityA == null) throw new ArgumentNullException(nameof(entityA));
            if (entityB == null) throw new ArgumentNullException(nameof(entityB));
            if (macroRelationType == null) throw new ArgumentNullException(nameof(macroRelationType));

            var relation = new Relation
            {
                Id = Guid.NewGuid(),
                ObjectAId = entityA.Id,
                ObjectBId = entityB.Id,
                RelationType = macroRelationType.RelationType.ToString(),
                RelationParams = parameters
            };

            return await _relationRepository.AddAsync(relation);
        }

        public async Task<Classes.BusinessEntity?> GetBusinessEntityById(Guid id)
        {
            _webLogger?.Debug($"GetBusinessEntityById: id={id}");
            return await _businessEntityRepository.GetByIdAsync(id);
        }

        public async Task<Relation?> GetRelationById(Guid id)
        {
            return await _relationRepository.GetByIdAsync(id);
        }

        public async Task<IReadOnlyList<Classes.BusinessEntity>> GetAllBusinessEntities(int? take = null)
        {
            return await _businessEntityRepository.GetAllAsync(null, take);
        }

        public async Task<IReadOnlyList<Relation>> GetAllRelations(int? take = null)
        {
            return await _relationRepository.GetAllAsync(null, take);
        }

        public async Task<IReadOnlyList<Relation>> GetRelationsByEntityIdAsync(Guid entityId, CancellationToken ct = default)
        {
            return await _relationRepository.GetAllAsync(r => r.ObjectAId == entityId || r.ObjectBId == entityId, ct: ct);
        }

        public async Task<IEnumerable<Classes.BusinessEntity>> GetContainedEntitiesAsync(Guid parentId)
        {
            var relations = await _relationRepository.GetAllAsync(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString());
            var childIds = relations.Select(r => r.ObjectBId).ToList();
            
            var children = new List<Classes.BusinessEntity>();
            foreach (var childId in childIds)
            {
                var child = await _businessEntityRepository.GetByIdAsync(childId);
                if (child != null)
                {
                    children.Add(child);
                }
            }
            
            // Сортируем дочерние элементы по дате создания
            return children.OrderBy(c => c.CreatedDate);
        }

        public async Task<IEnumerable<Classes.BusinessEntity>> GetRootEntitiesAsync()
        {
            // Находим все сущности, которые НЕ являются объектом B в отношении "Contains"
            var allEntities = await _businessEntityRepository.GetAllAsync();
            var containsRelations = await _relationRepository.GetAllAsync(r => r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString());
            var childIds = containsRelations.Select(r => r.ObjectBId).ToHashSet();
            
            var rootEntities = allEntities.Where(e => !childIds.Contains(e.Id));
            
            // Сортируем корневые элементы по дате создания
            return rootEntities.OrderBy(e => e.CreatedDate);
        }
    }
} 