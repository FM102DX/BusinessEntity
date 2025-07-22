using System;
using System.Threading.Tasks;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System.Linq;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Core.Services
{
    /// <summary>
    /// Хелпер для работы с бизнес-сущностями
    /// </summary>
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

        /// <summary>
        /// Получает все бизнес-сущности, содержащиеся в родительской сущности
        /// </summary>
        public async Task<IEnumerable<Classes.BusinessEntity>> GetContainedEntitiesAsync(Guid parentId, CancellationToken ct = default)
        {
            var relations = await _relationRepository.GetAllAsync(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString(), ct: ct);
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

        /// <summary>
        /// Генерирует новое имя для элемента на основе типа и существующих элементов у родителя
        /// </summary>
        private async Task<string> GetNewItemNameAsync(
            Classes.BusinessEntity parent,
            BusinessEntityTypeEnum newType,
            CancellationToken ct = default)
        {
            var baseName = $"New{newType}"; // "NewFolder"
            var children = await GetContainedEntitiesAsync(parent.Id, ct);
            var sameTypeChildren = children.Where(c => c.EntityType == newType)
                                          .Select(c => c.Name)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Ищем свободный суффикс
            for (var i = 1; ; i++)
            {
                var candidateName = $"{baseName}{i}";
                if (!sameTypeChildren.Contains(candidateName))
                    return candidateName;
            }
        }

        /// <summary>
        /// Создает новую подпапку внутри родительской папки или пространства
        /// </summary>
        public async Task<Classes.BusinessEntity> CreateSubFolderAsync(
            Classes.BusinessEntity parent,
            CancellationToken ct = default)
        {
            if (parent.EntityType != BusinessEntityTypeEnum.Folder && parent.EntityType != BusinessEntityTypeEnum.Space)
            {
                throw new ArgumentException("Parent must be a Folder or Space", nameof(parent));
            }

            // Генерируем уникальное имя
            var name = await GetNewItemNameAsync(parent, BusinessEntityTypeEnum.Folder, ct);

            // Создаем новую сущность
            var entity = new Classes.BusinessEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                EntityType = BusinessEntityTypeEnum.Folder,
                BusinessEntityType = BusinessEntityTypeEnum.Folder
            };

            // Сохраняем сущность
            await _businessEntityRepository.AddAsync(entity, ct);

            // Создаем связь между родителем и дочерним элементом
            var relation = new Relation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.Contains.ToString(),
                RelationParams = ""
            };

            // Сохраняем связь
            await _relationRepository.AddAsync(relation, ct);

            _webLogger?.Information($"Created new folder '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }
    }
} 