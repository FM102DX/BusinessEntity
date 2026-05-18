using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using System.Linq;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.WebLogger.Services;
using BusinessEntity.Core.BaseClasses.Relations;

// Основной сервис работы с сущностями, data и связями
namespace BusinessEntity.Core.Services
{
    // Содержит прикладные операции поверх DataProvider
    public class BusinessEntityHelper
    {
        // Коннектор хранения entity, data и relations
        private readonly IDataProviderConnector _dataProviderConnector;
        // Логгер бизнес-операций
        private readonly IWebLoggerService? _webLogger;
        // Фабрика для создания entity и typed entity
        private readonly IBusinessEntityFactory _businessEntityFactory;

        // Подключает зависимости для работы с бизнес-сущностями
        public BusinessEntityHelper(
            IDataProviderConnector dataProviderConnector,
            IWebLoggerService? webLogger,
            IBusinessEntityFactory businessEntityFactory)
        {
            _dataProviderConnector = dataProviderConnector ?? throw new ArgumentNullException(nameof(dataProviderConnector));
            _webLogger = webLogger; // Логгер может быть не настроен, поэтому допускаем null
            _businessEntityFactory = businessEntityFactory ?? throw new ArgumentNullException(nameof(businessEntityFactory));
        }

        // Создает простую сущность и сохраняет ее в хранилище
        public async Task<Classes.BusinessEntity> CreateBusinessEntity(BusinessEntityTypeEnum type, string name)
        {
            _webLogger?.Information($"CreateBusinessEntity: type={type}, name={name}");
            var entity = CreateEntityForType(type, name);
            _webLogger?.Information($"[мини-апп:business-entity-helper] [entity:create] [dispatch:add-entity] Подготовлена сущность type={type} id={entity.Id} name='{entity.Name}'");

            return await _dataProviderConnector.AddAsync(entity);
        }

        // Удаляет сущность вместе со всеми ее связями
        public async Task RemoveBusinessEntity(Guid id)
        {
            _webLogger?.Warning($"RemoveBusinessEntity: id={id}");
            // Сначала удаляем все связи, где участвует данная сущность
            var relations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r => r.ObjectAId == id || r.ObjectBId == id)
                .ToList();
            
            // Затем удаляем найденные связи
            foreach (var relation in relations)
            {
                await _dataProviderConnector.DeleteRelationAsync(relation.Id);
            }

            // Затем удаляем саму сущность
            await _dataProviderConnector.DeleteAsync(id);
        }

        // Создает relation между двумя сущностями
        public async Task<BusinessEntityRelation> CreateRelation(IBusinessEntity entityA, IBusinessEntity entityB, MacroRelationType macroRelationType, string parameters = "")
        {
            _webLogger?.Information($"CreateRelation: {entityA?.Name} -> {entityB?.Name} relation={macroRelationType?.RelationType}");
            if (entityA == null) throw new ArgumentNullException(nameof(entityA));
            if (entityB == null) throw new ArgumentNullException(nameof(entityB));
            if (macroRelationType == null) throw new ArgumentNullException(nameof(macroRelationType));

            var relation = new BusinessEntityRelation
            {
                Id = Guid.NewGuid(),
                ObjectAId = entityA.Id,
                ObjectBId = entityB.Id,
                RelationType = macroRelationType.RelationType.ToString(),
                RelationParams = parameters
            };

            return await _dataProviderConnector.CreateRelationAsync(relation);
        }

        // Удаляет все связи выбранного типа между двумя сущностями
        public async Task<int> RemoveRelation(IBusinessEntity entityA, IBusinessEntity entityB, MacroRelationType macroRelationType)
        {
            _webLogger?.Information($"RemoveRelation: {entityA?.Name} -> {entityB?.Name} relation={macroRelationType?.RelationType}");
            if (entityA == null) throw new ArgumentNullException(nameof(entityA));
            if (entityB == null) throw new ArgumentNullException(nameof(entityB));
            if (macroRelationType == null) throw new ArgumentNullException(nameof(macroRelationType));

            // Находим все связи такого типа между сущностями А и Б
            var relationsToRemove = (await _dataProviderConnector.GetRelationsAsync(entityA.Id, entityB.Id))
                .Where(r => r.RelationType == macroRelationType.RelationType.ToString())
                .ToList();

            // Поочередно удаляем найденные relation
            int removedCount = 0;
            foreach (var relation in relationsToRemove)
            {
                await _dataProviderConnector.DeleteRelationAsync(relation.Id);
                removedCount++;
                _webLogger?.Debug($"Removed relation: ID={relation.Id}, Type={relation.RelationType}");
            }

            _webLogger?.Information($"Removed {removedCount} relations of type {macroRelationType.RelationType} between {entityA.Name} and {entityB.Name}");
            return removedCount;
        }

        // Возвращает entity по идентификатору
        public async Task<Classes.BusinessEntity?> GetBusinessEntityById(Guid id)
        {
            _webLogger?.Debug($"GetBusinessEntityById: id={id}");
            return await _dataProviderConnector.GetByIdAsync(id);
        }

        // Возвращает relation по идентификатору
        public async Task<BusinessEntityRelation?> GetRelationById(Guid id)
        {
            return await _dataProviderConnector.GetRelationByIdAsync(id);
        }

        // Возвращает все entity с необязательным ограничением по количеству
        public async Task<IReadOnlyList<Classes.BusinessEntity>> GetAllBusinessEntities(int? take = null)
        {
            var entities = await _dataProviderConnector.GetAllAsync();
            return take.HasValue ? entities.Take(take.Value).ToList() : entities;
        }

        // Возвращает только пространства
        public async Task<IReadOnlyList<Classes.BusinessEntity>> GetSpacesAsync(int? take = null)
        {
            var spaces = (await _dataProviderConnector.GetAllAsync())
                .Where(e => e.EntityType == BusinessEntityTypeEnum.Space);
            return take.HasValue ? spaces.Take(take.Value).ToList() : spaces.ToList();
        }

        // Возвращает все relation с необязательным ограничением по количеству
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelations(int? take = null)
        {
            var relations = await _dataProviderConnector.GetAllRelationsAsync();
            return take.HasValue ? relations.Take(take.Value).ToList() : relations;
        }

        // Возвращает все relation, где участвует указанная entity
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsByEntityIdAsync(Guid entityId, CancellationToken ct = default)
        {
            return (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == entityId || r.ObjectBId == entityId)
                .ToList();
        }

        // Возвращает дочерние entity по связи Contains
        public async Task<IEnumerable<Classes.BusinessEntity>> GetContainedEntitiesAsync(Guid parentId, CancellationToken ct = default)
        {
            // Ищем relation вида родитель -> ребенок
            var relations = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
                .ToList();
            var childIds = relations.Select(r => r.ObjectBId).ToList();
            
            // Загружаем дочерние entity по найденным Id
            var children = new List<Classes.BusinessEntity>();
            foreach (var childId in childIds)
            {
                var child = await _dataProviderConnector.GetByIdAsync(childId);
                if (child != null)
                {
                    children.Add(child);
                }
            }
            
            // Сортируем дочерние элементы по дате создания
            return children.OrderBy(c => c.CreatedDate);
        }

        // Возвращает корневые entity, у которых нет родителя по Contains
        public async Task<IEnumerable<Classes.BusinessEntity>> GetRootEntitiesAsync()
        {
            // Находим все сущности, которые НЕ являются объектом B в отношении "Contains"
            var allEntities = await _dataProviderConnector.GetAllAsync();
            var containsRelations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r => r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
                .ToList();
            var childIds = containsRelations.Select(r => r.ObjectBId).ToHashSet();
            
            var rootEntities = allEntities.Where(e => !childIds.Contains(e.Id));
            
            // Сортируем корневые элементы по дате создания
            return rootEntities.OrderBy(e => e.CreatedDate);
        }

        // Генерирует уникальное имя нового дочернего элемента
        private async Task<string> GetNewItemNameAsync(
            Classes.BusinessEntity parent,
            BusinessEntityTypeEnum newType,
            CancellationToken ct = default)
        {
            // Формируем базовое имя по типу
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

        // Создает новую подпапку внутри папки или пространства
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
            var entity = CreateEntityForType(BusinessEntityTypeEnum.Folder, name);

            // Сохраняем сущность
            // _webLogger?.Information($"[мини-апп:business-entity-helper] [entity:create] [folder] Создаем folder id={entity.Id} name='{name}' parentId={parent.Id} parentName='{parent.Name}'");
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем связь Contains родитель -> дочерний элемент
            var relation = new BusinessEntityRelation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.Contains.ToString(),
                RelationParams = ""
            };

            // Сохраняем связь
            // _webLogger?.Information($"[мини-апп:business-entity-helper] [relation:create] [dispatch:add-relation] Folder parent-child relation parentId={parent.Id} childId={entity.Id} type={relation.RelationType}");
            await _dataProviderConnector.CreateRelationAsync(relation, cancellationToken: ct);

            _webLogger?.Information($"Created new folder '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }

        // Блок операций создания документа
        #region Document operations
        // Создает новый документ с текстом по умолчанию
        public async Task<Classes.BusinessEntity> CreateDocumentAsync(
            Classes.BusinessEntity parent,
            CancellationToken ct = default)
        {
            // Backwards-compatible wrapper: create document with default body
            return await CreateDocumentAsync(parent, null, ct);
        }

        // Создает новый документ с заданным текстом
        public async Task<Classes.BusinessEntity> CreateDocumentAsync(
            Classes.BusinessEntity parent,
            string? bodyText,
            CancellationToken ct = default)
        {
            if (parent.EntityType != BusinessEntityTypeEnum.Folder && parent.EntityType != BusinessEntityTypeEnum.Space)
            {
                throw new ArgumentException("Parent must be a Folder or Space", nameof(parent));
            }

            // Генерируем уникальное имя документа
            var name = await GetNewItemNameAsync(parent, BusinessEntityTypeEnum.Document, ct);

            // Определяем текст для сохранения
            var dataToSave = string.IsNullOrWhiteSpace(bodyText) ? new string('x', 100) : bodyText;
            var documentData = new Document
            {
                Name = name,
                Tag = BusinessEntityTypeEnum.Document.ToString(),
                Text = dataToSave
            };

            // Создаем runtime-объект документа через фабрику
            var entity = CreateEntityForType(BusinessEntityTypeEnum.Document, name, dataToSave);
            // _webLogger?.Information($"[мини-апп:business-entity-helper] [entity:create] [document] Создаем document id={entity.Id} name='{name}' parentId={parent.Id} parentName='{parent.Name}' payloadLength={dataToSave.Length}");

            // Сохраняем сущность
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем связь Contains с родителем
            var relation = new BusinessEntityRelation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.Contains.ToString(),
                RelationParams = string.Empty
            };

            // _webLogger?.Information($"[мини-апп:business-entity-helper] [relation:create] [dispatch:add-relation] Document parent-child relation parentId={parent.Id} childId={entity.Id} type={relation.RelationType}");
            await _dataProviderConnector.CreateRelationAsync(relation, cancellationToken: ct);

            // Создаем payload документа
            // _webLogger?.Information($"[мини-апп:business-entity-helper] [entity-data:create] [dispatch:update-data] Создаем payload для document entityId={entity.Id} length={dataToSave.Length}");
            await _dataProviderConnector.UpdateDataAsync(entity.Id, documentData, ct);

            _webLogger?.Debug($"Created BusinessEntityData for document '{name}' (DocID: {entity.Id}), DataLength={dataToSave?.Length ?? 0}");

            _webLogger?.Information($"Created new document '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }
        #endregion

        // Меняет родителя элемента в дереве по связи Contains
        public async Task ChangeVisualFolderParentForItem(Classes.BusinessEntity child, Classes.BusinessEntity newVisualParent)
        {
            _webLogger?.Information($"ChangeVisualFolderParentForItem: Moving '{child?.Name}' to new visual parent '{newVisualParent?.Name}'");
            
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (newVisualParent == null) throw new ArgumentNullException(nameof(newVisualParent));

            // Проверяем, не создаст ли перемещение цикл
            if (await WouldCreateCyclicDependency(child.Id, newVisualParent.Id))
            {
                var errorMessage = $"Cannot move '{child.Name}' to '{newVisualParent.Name}': this would create a cyclic dependency";
                _webLogger?.Warning(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Находим текущих родителей элемента по связи Contains
            var currentVisualParentRelations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r =>
                    r.ObjectBId == child.Id &&
                    r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
                .ToList();

            // Создаем макро-описание нужной связи
            var containsRelationType = new MacroRelationType
            {
                RelationType = BusinessEntityRelationTypeEnum.Contains
            };

            // Удаляем старые связи Contains
            foreach (var currentRelation in currentVisualParentRelations)
            {
                var currentParent = await _dataProviderConnector.GetByIdAsync(currentRelation.ObjectAId);
                if (currentParent != null)
                {
                    _webLogger?.Debug($"Removing contains relation between '{currentParent.Name}' and '{child.Name}'");
                    await RemoveRelation(currentParent, child, containsRelationType);
                }
            }

            // Создаем новую связь Contains
            await CreateRelation(newVisualParent, child, containsRelationType);
            
            _webLogger?.Information($"Successfully moved '{child.Name}' to new visual parent '{newVisualParent.Name}'");
        }

        // Полностью удаляет сущность и ее дочернее поддерево
        public async Task<(bool success, List<string> messages)> RemoveBusinessEntityPermanently(Guid entityId, CancellationToken ct = default)
        {
            var messages = new List<string>();
            
            // Проверяем, существует ли сущность
            var entity = await _dataProviderConnector.GetByIdAsync(entityId);
            if (entity == null)
            {
                // Если сущность не найдена, считаем удаление успешным
                return (true, new List<string>());
            }
            
            // Сначала рекурсивно удаляем всех потомков
            var childrenRemovalResult = await RemoveChildrenRecursively(entityId, ct);
            if (!childrenRemovalResult.success)
            {
                // Если не удалось удалить детей, не удаляем родителя
                return childrenRemovalResult;
            }
            
            // Проверяем, можно ли удалить саму сущность
            var canDelete = await CanDelete(entityId, ct);
            if (!canDelete)
            {
                messages.Add($"Не удается удалить бизнес-энтити '{entity.Name}' (ID: {entityId})");
                return (false, messages);
            }
            
            // Удаляем все связи сущности
            await RemoveAllEntityRelations(entityId, ct);
            
            // Удаляем саму сущность
            await _dataProviderConnector.DeleteAsync(entityId, ct);
            
            return (true, new List<string>());
        }
        
        // Рекурсивно удаляет всех потомков сущности по связи Contains
        private async Task<(bool success, List<string> messages)> RemoveChildrenRecursively(Guid parentId, CancellationToken ct = default)
        {
            var allMessages = new List<string>();
            
            // Получаем всех прямых потомков по связи Contains
            var childRelations = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
                .ToList();
            
            foreach (var relation in childRelations)
            {
                var childId = relation.ObjectBId;
                
                // Проверяем, существует ли дочерняя сущность
                var childEntity = await _dataProviderConnector.GetByIdAsync(childId, cancellationToken: ct);
                if (childEntity == null)
                {
                    // Если дочерняя сущность не найдена, просто продолжаем
                    continue;
                }
                
                // Сначала удаляем потомков ребенка
                var childrenResult = await RemoveChildrenRecursively(childId, ct);
                if (!childrenResult.success)
                {
                    // Добавляем сообщения от неудачного удаления потомков
                    allMessages.AddRange(childrenResult.messages);
                    return (false, allMessages);
                }
                
                // Проверяем, можно ли удалить самого ребенка
                var canDeleteChild = await CanDelete(childId, ct);
                if (!canDeleteChild)
                {
                    allMessages.Add($"Не удается удалить дочернюю бизнес-энтити '{childEntity.Name}' (ID: {childId})");
                    return (false, allMessages);
                }
                
                // Удаляем все связи дочерней сущности
                await RemoveAllEntityRelations(childId, ct);
                
                // Удаляем саму дочернюю сущность
                await _dataProviderConnector.DeleteAsync(childId, ct);
            }
            
            return (true, allMessages);
        }
        
        // Проверяет возможность удаления сущности
        private async Task<bool> CanDelete(Guid entityId, CancellationToken ct = default)
        {
            // Пока это заглушка для будущих бизнес-правил
            await Task.CompletedTask;
            return true;
        }
        
        // Удаляет все relation, в которых участвует сущность
        private async Task RemoveAllEntityRelations(Guid entityId, CancellationToken ct = default)
        {
            // Получаем все связи, где сущность является ObjectA
            var relationsAsA = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == entityId)
                .ToList();
            
            // Получаем все связи, где сущность является ObjectB
            var relationsAsB = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectBId == entityId)
                .ToList();
            
            // Удаляем все найденные связи
            foreach (var relation in relationsAsA.Concat(relationsAsB))
            {
                await _dataProviderConnector.DeleteRelationAsync(relation.Id, ct);
            }
        }

        // Переименовывает сущность и сохраняет изменение
        public async Task<Classes.BusinessEntity?> RenameEntity(Guid entityId, string newName, CancellationToken ct = default)
        {
            _webLogger?.Information($"RenameEntity: entityId={entityId}, newName='{newName}'");
            
            if (string.IsNullOrWhiteSpace(newName))
            {
                _webLogger?.Warning($"RenameEntity: новое имя не может быть пустым");
                return null;
            }
            
            // Получаем сущность
            var entity = await _dataProviderConnector.GetByIdAsync(entityId, cancellationToken: ct);
            if (entity == null)
            {
                _webLogger?.Warning($"RenameEntity: сущность с ID {entityId} не найдена");
                return null;
            }
            
            // Обновляем имя
            entity.Name = newName.Trim();
            
            // Сохраняем изменения
            await _dataProviderConnector.UpdateAsync(entity, cancellationToken: ct);
            _webLogger?.Information($"RenameEntity: сущность успешно переименована в '{newName}'");
            
            return entity;
        }

        // Проверяет, создаст ли перенос циклическую зависимость
        public async Task<bool> WouldCreateCyclicDependency(Guid childId, Guid newParentId)
        {
            // Если пытаемся переместить элемент в самого себя
            if (childId == newParentId)
                return true;

            // Проверяем, является ли newParent потомком child
            return await IsDescendantInDatabase(newParentId, childId);
        }

        // Проверяет, лежит ли один узел внутри поддерева другого
        private async Task<bool> IsDescendantInDatabase(Guid descendantId, Guid ancestorId)
        {
            // Получаем родителя descendantId
            var parentRelations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r =>
                    r.ObjectBId == descendantId &&
                    r.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
                .ToList();

            foreach (var relation in parentRelations)
            {
                var parentId = relation.ObjectAId;
                
                // Если родитель - это ancestor, то descendant является потомком ancestor
                if (parentId == ancestorId)
                    return true;

                // Рекурсивно проверяем выше по иерархии
                if (await IsDescendantInDatabase(parentId, ancestorId))
                    return true;
            }

            return false;
        }

        // Загружает payload-объекты для сущности
        public async Task<IReadOnlyList<BusinessEntityData>> GetData(Classes.BusinessEntity entityData)
        {
            if (entityData == null) throw new ArgumentNullException(nameof(entityData));
            _webLogger?.Debug($"GetData: entityId={entityData.Id}, type={entityData.EntityType}");

            switch (entityData.EntityType)
            {
                case BusinessEntityTypeEnum.Space:
                    return await LoadTypedDataListAsync<Space>(entityData);
                case BusinessEntityTypeEnum.Folder:
                    return await LoadTypedDataListAsync<Folder>(entityData);
                case BusinessEntityTypeEnum.Document:
                    return await LoadTypedDataListAsync<Document>(entityData);
                case BusinessEntityTypeEnum.RichTextDocument:
                    return await LoadTypedDataListAsync<RichTextDocument>(entityData);
                case BusinessEntityTypeEnum.MediaVideo:
                    return await LoadTypedDataListAsync<MediaVideo>(entityData);
                case BusinessEntityTypeEnum.SysParametersTp:
                    return await LoadTypedDataListAsync<SysParameters>(entityData);
                default:
                    return Array.Empty<BusinessEntityData>();
            }
        }

        // Загружает typed-сущность вместе с ее payload-объектом
        public async Task<Classes.BusinessEntity<TData>?> GetEntityWithDataAsync<TData>(Guid entityId, CancellationToken ct = default)
            where TData : class, IBusinessEntityData, new()
        {
            var entity = await _dataProviderConnector.GetByIdAsync(entityId, cancellationToken: ct);
            if (entity == null)
            {
                return null;
            }

            var data = await _dataProviderConnector.GetDataAsync<TData>(entity.Id, ct) ?? new TData();
            var typedEntity = _businessEntityFactory.Create(entity.EntityType, data, entity.Name);
            typedEntity = CopyEntityState(entity, typedEntity);

            if (string.IsNullOrWhiteSpace(typedEntity.Data.Name))
            {
                typedEntity.Data.Name = typedEntity.Name;
            }

            if (typedEntity.Data.EntityType == BusinessEntityTypeEnum.Undefined)
            {
                typedEntity.Data.EntityType = typedEntity.EntityType;
            }

            if (string.IsNullOrWhiteSpace(typedEntity.Data.Tag))
            {
                typedEntity.Data.Tag = typedEntity.EntityType.ToString();
            }

            return typedEntity;
        }

        // Возвращает singleton-объект указанного типа; если его нет — создает и сохраняет
        public async Task<Classes.BusinessEntity<TData>> GetOrCreateSingletonEntityAsync<TData>(
            BusinessEntityTypeEnum type,
            string name,
            CancellationToken ct = default)
            where TData : class, IBusinessEntityData, new()
        {
            var existingEntity = (await GetAllBusinessEntities())
                .Where(x => x.EntityType == type && string.Equals(x.Name, name, StringComparison.Ordinal))
                .OrderBy(x => x.CreatedDate)
                .FirstOrDefault();

            if (existingEntity != null)
            {
                var typedExistingEntity = await GetEntityWithDataAsync<TData>(existingEntity.Id, ct);
                if (typedExistingEntity != null)
                {
                    return typedExistingEntity;
                }
            }

            var createdEntity = _businessEntityFactory.Create<TData>(type, name);
            createdEntity.Name = name;
            createdEntity.Data.Name = name;
            createdEntity.Data.Tag = type.ToString();

            return await SaveEntityAsync(createdEntity, ct);
        }

        // Сохраняет typed business-объект как entity + payload без relation
        public async Task<Classes.BusinessEntity<TData>> SaveEntityAsync<TData>(
            Classes.BusinessEntity<TData> entity,
            CancellationToken ct = default)
            where TData : class, IBusinessEntityData
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Data == null) throw new ArgumentNullException(nameof(entity.Data));

            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            if (entity.CreatedDate == default)
            {
                entity.CreatedDate = DateTime.UtcNow;
            }

            entity.LastModifiedDate = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(entity.Data.Name))
            {
                entity.Data.Name = entity.Name;
            }

            if (entity.Data.EntityType == BusinessEntityTypeEnum.Undefined)
            {
                entity.Data.EntityType = entity.EntityType;
            }

            if (string.IsNullOrWhiteSpace(entity.Data.Tag))
            {
                entity.Data.Tag = entity.EntityType.ToString();
            }

            entity.SynchronizeDataWithEntity();

            var persistenceEntity = CopyEntityState(entity, _businessEntityFactory.Create(entity.EntityType, entity.Name));
            var existingEntity = await _dataProviderConnector.GetByIdAsync(entity.Id, cancellationToken: ct);

            if (existingEntity != null)
            {
                await _dataProviderConnector.UpdateAsync(persistenceEntity, cancellationToken: ct);
            }
            else
            {
                await _dataProviderConnector.AddAsync(persistenceEntity, cancellationToken: ct);
            }

            await _dataProviderConnector.UpdateDataAsync(entity.Id, entity.Data, ct);
            return entity;
        }

        // Сохраняет entity и ее payload в хранилище
        public async Task SaveEntity(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            if (entityData == null) throw new ArgumentNullException(nameof(entityData));
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Всегда выравниваем identity и тип перед сохранением
            data.Id = entityData.Id;
            data.EntityType = entityData.EntityType;

            var entityToSave = CreatePersistenceEntity(entityData, data);

            _webLogger?.Information($"SaveEntity: entityId={entityData.Id}, name='{entityData.Name}', dataType='{data.GetType().Name}'");

            // Сохраняем саму entity
            var existingEntity = await _dataProviderConnector.GetByIdAsync(entityToSave.Id);
            if (existingEntity != null)
            {
                await _dataProviderConnector.UpdateAsync(entityToSave);
            }
            else
            {
                await _dataProviderConnector.AddAsync(entityToSave);
            }

            // Сохраняем typed payload сущности отдельно через формализованный converter-path mini-app.
            await SaveTypedDataAsync(entityToSave.Id, data);

            _webLogger?.Debug($"SaveEntity: saved entityData {entityData.Id} and data {data.Id}");
        }

        // Создает runtime-entity нужного типа через фабрику
        private Classes.BusinessEntity CreateEntityForType(BusinessEntityTypeEnum type, string name, string? payload = null)
        {
            return type switch
            {
                BusinessEntityTypeEnum.Document => _businessEntityFactory.Create(
                    type,
                    new Document
                    {
                        Name = name,
                        Text = payload ?? string.Empty
                    },
                    name),
                BusinessEntityTypeEnum.Folder => _businessEntityFactory.Create<Folder>(type, name),
                BusinessEntityTypeEnum.Space => _businessEntityFactory.Create<Space>(type, name),
                BusinessEntityTypeEnum.MediaVideo => _businessEntityFactory.Create<MediaVideo>(type, name),
                BusinessEntityTypeEnum.SysParametersTp => _businessEntityFactory.Create<SysParameters>(type, name),
                _ => _businessEntityFactory.Create(type, name)
            };
        }

        // Подготавливает entity к сохранению через connector
        private Classes.BusinessEntity CreatePersistenceEntity(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            var entityType = ResolveEntityType(entityData, data);

            return data switch
            {
                Document document => CreateDocumentPersistenceEntity(entityData, document, entityType),
                RichTextDocument richTextDocument => CreateRichTextDocumentPersistenceEntity(entityData, richTextDocument, entityType),
                MediaVideo mediaVideo => CreateMediaVideoPersistenceEntity(entityData, mediaVideo, entityType),
                SysParameters sysParameters => CreateSysParametersPersistenceEntity(entityData, sysParameters, entityType),
                Folder => CopyEntityState(entityData, _businessEntityFactory.Create<Folder>(entityType, entityData.Name)),
                Space => CopyEntityState(entityData, _businessEntityFactory.Create<Space>(entityType, entityData.Name)),
                _ => CopyEntityState(entityData, _businessEntityFactory.Create(entityType, entityData.Name))
            };
        }

        // Создает entity документа для сохранения
        private Classes.BusinessEntity CreateDocumentPersistenceEntity(Classes.BusinessEntity entityData, Document document, BusinessEntityTypeEnum entityType)
        {
            var typedEntity = _businessEntityFactory.Create(
                entityType,
                new Document
                {
                    Name = string.IsNullOrWhiteSpace(document.Name) ? entityData.Name : document.Name,
                    Tag = document.Tag,
                    PublishedVersion = document.PublishedVersion,
                    Text = document.Text ?? string.Empty
                },
                entityData.Name);

            typedEntity = CopyEntityState(entityData, typedEntity);
            typedEntity.Data.Tag = document.Tag;
            typedEntity.Data.PublishedVersion = document.PublishedVersion;
            typedEntity.Data.Text = document.Text ?? string.Empty;

            return typedEntity;
        }

        // Создает entity rich-text документа для сохранения manifest-а без потери storage-настроек.
        private Classes.BusinessEntity CreateRichTextDocumentPersistenceEntity(Classes.BusinessEntity entityData, RichTextDocument richTextDocument, BusinessEntityTypeEnum entityType)
        {
            var typedEntity = _businessEntityFactory.Create(
                entityType,
                new RichTextDocument
                {
                    Name = string.IsNullOrWhiteSpace(richTextDocument.Name) ? entityData.Name : richTextDocument.Name,
                    Tag = richTextDocument.Tag,
                    ContentStorage = richTextDocument.ContentStorage,
                    EditorFormat = richTextDocument.EditorFormat,
                    ChunkPolicy = richTextDocument.ChunkPolicy,
                    EmbeddedFileStorage = richTextDocument.EmbeddedFileStorage,
                    SupportsImages = richTextDocument.SupportsImages,
                    PublishedVersion = richTextDocument.PublishedVersion
                },
                entityData.Name);

            typedEntity = CopyEntityState(entityData, typedEntity);
            typedEntity.Data.Tag = richTextDocument.Tag;
            typedEntity.Data.ContentStorage = richTextDocument.ContentStorage;
            typedEntity.Data.EditorFormat = richTextDocument.EditorFormat;
            typedEntity.Data.ChunkPolicy = richTextDocument.ChunkPolicy;
            typedEntity.Data.EmbeddedFileStorage = richTextDocument.EmbeddedFileStorage;
            typedEntity.Data.SupportsImages = richTextDocument.SupportsImages;
            typedEntity.Data.PublishedVersion = richTextDocument.PublishedVersion;

            return typedEntity;
        }

        // Создает entity видео для сохранения typed payload без потери storage-полей.
        private Classes.BusinessEntity CreateMediaVideoPersistenceEntity(Classes.BusinessEntity entityData, MediaVideo mediaVideo, BusinessEntityTypeEnum entityType)
        {
            var typedEntity = _businessEntityFactory.Create(
                entityType,
                new MediaVideo
                {
                    Name = string.IsNullOrWhiteSpace(mediaVideo.Name) ? entityData.Name : mediaVideo.Name,
                    Tag = mediaVideo.Tag,
                    FileName = mediaVideo.FileName ?? string.Empty,
                    DisplayName = mediaVideo.DisplayName ?? string.Empty,
                    ContentType = mediaVideo.ContentType ?? "application/octet-stream",
                    OriginalSizeBytes = mediaVideo.OriginalSizeBytes,
                    DurationSeconds = mediaVideo.DurationSeconds,
                    UploadedByUserId = mediaVideo.UploadedByUserId,
                    UploadedDate = mediaVideo.UploadedDate,
                    StorageRelativePath = mediaVideo.StorageRelativePath ?? string.Empty,
                    EmbedUrl = mediaVideo.EmbedUrl ?? string.Empty,
                    Comment = mediaVideo.Comment ?? string.Empty
                },
                entityData.Name);

            typedEntity = CopyEntityState(entityData, typedEntity);
            typedEntity.Data.Tag = mediaVideo.Tag;
            typedEntity.Data.FileName = mediaVideo.FileName ?? string.Empty;
            typedEntity.Data.DisplayName = mediaVideo.DisplayName ?? string.Empty;
            typedEntity.Data.ContentType = mediaVideo.ContentType ?? "application/octet-stream";
            typedEntity.Data.OriginalSizeBytes = mediaVideo.OriginalSizeBytes;
            typedEntity.Data.DurationSeconds = mediaVideo.DurationSeconds;
            typedEntity.Data.UploadedByUserId = mediaVideo.UploadedByUserId;
            typedEntity.Data.UploadedDate = mediaVideo.UploadedDate;
            typedEntity.Data.StorageRelativePath = mediaVideo.StorageRelativePath ?? string.Empty;
            typedEntity.Data.EmbedUrl = mediaVideo.EmbedUrl ?? string.Empty;
            typedEntity.Data.Comment = mediaVideo.Comment ?? string.Empty;

            return typedEntity;
        }

        // Создает entity системных параметров для сохранения typed payload без потери прикладных полей.
        private Classes.BusinessEntity CreateSysParametersPersistenceEntity(Classes.BusinessEntity entityData, SysParameters sysParameters, BusinessEntityTypeEnum entityType)
        {
            var typedEntity = _businessEntityFactory.Create(
                entityType,
                new SysParameters
                {
                    Name = string.IsNullOrWhiteSpace(sysParameters.Name) ? entityData.Name : sysParameters.Name,
                    Tag = sysParameters.Tag,
                    CompanyName = sysParameters.CompanyName ?? string.Empty,
                    RichTextChunkCharLimit = sysParameters.RichTextChunkCharLimit,
                    RichTextInitialChunkCount = sysParameters.RichTextInitialChunkCount,
                    RichTextTableOfContentsBeforeBuffer = sysParameters.RichTextTableOfContentsBeforeBuffer,
                    RichTextTableOfContentsAfterBuffer = sysParameters.RichTextTableOfContentsAfterBuffer,
                    RichTextScrollPreviousChunkCount = sysParameters.RichTextScrollPreviousChunkCount,
                    RichTextHideTableOfContentsScrollbar = sysParameters.RichTextHideTableOfContentsScrollbar
                },
                entityData.Name);

            typedEntity = CopyEntityState(entityData, typedEntity);
            typedEntity.Data.Tag = sysParameters.Tag;
            typedEntity.Data.CompanyName = sysParameters.CompanyName ?? string.Empty;
            typedEntity.Data.RichTextChunkCharLimit = sysParameters.RichTextChunkCharLimit;
            typedEntity.Data.RichTextInitialChunkCount = sysParameters.RichTextInitialChunkCount;
            typedEntity.Data.RichTextTableOfContentsBeforeBuffer = sysParameters.RichTextTableOfContentsBeforeBuffer;
            typedEntity.Data.RichTextTableOfContentsAfterBuffer = sysParameters.RichTextTableOfContentsAfterBuffer;
            typedEntity.Data.RichTextScrollPreviousChunkCount = sysParameters.RichTextScrollPreviousChunkCount;
            typedEntity.Data.RichTextHideTableOfContentsScrollbar = sysParameters.RichTextHideTableOfContentsScrollbar;

            return typedEntity;
        }

        // Копирует метаданные существующей entity в новую runtime-entity
        private static Classes.BusinessEntity CopyEntityState(Classes.BusinessEntity source, Classes.BusinessEntity target)
        {
            target.Id = source.Id;
            target.CreatedDate = source.CreatedDate;
            target.LastModifiedDate = source.LastModifiedDate;
            target.CreatedByUserId = source.CreatedByUserId;
            target.LastModifiedByUserId = source.LastModifiedByUserId;
            target.IsPublic = source.IsPublic;
            target.Name = source.Name;
            target.BusinessEntityType = source.BusinessEntityType;
            target.EntityType = source.EntityType;

            return target;
        }

        // Копирует метаданные существующей entity в typed runtime-entity
        private static Classes.BusinessEntity<TData> CopyEntityState<TData>(Classes.BusinessEntity source, Classes.BusinessEntity<TData> target)
            where TData : class, IBusinessEntityData
        {
            target.Id = source.Id;
            target.CreatedDate = source.CreatedDate;
            target.LastModifiedDate = source.LastModifiedDate;
            target.CreatedByUserId = source.CreatedByUserId;
            target.LastModifiedByUserId = source.LastModifiedByUserId;
            target.IsPublic = source.IsPublic;
            target.Name = source.Name;
            target.BusinessEntityType = source.BusinessEntityType;
            target.EntityType = source.EntityType;
            target.SynchronizeDataWithEntity();

            return target;
        }

        // Выбирает итоговый тип entity для сохранения
        private static BusinessEntityTypeEnum ResolveEntityType(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            if (entityData.EntityType != BusinessEntityTypeEnum.Undefined)
            {
                return entityData.EntityType;
            }

            if (data.EntityType != BusinessEntityTypeEnum.Undefined)
            {
                return data.EntityType;
            }

            return entityData.BusinessEntityType;
        }

        // Загружает typed payload конкретного типа и возвращает его как единичный data-список для старого API helper-а.
        private async Task<IReadOnlyList<BusinessEntityData>> LoadTypedDataListAsync<TData>(Classes.BusinessEntity entityData)
            where TData : BusinessEntityData, IBusinessEntityData, new()
        {
            var data = await _dataProviderConnector.GetDataAsync<TData>(entityData.Id);
            if (data == null)
            {
                return Array.Empty<BusinessEntityData>();
            }

            ApplyEntityMetadata(entityData, data);
            return new BusinessEntityData[] { data };
        }

        // Сохраняет runtime data-объект через typed payload-путь data-provider mini-app.
        private Task SaveTypedDataAsync(Guid entityId, BusinessEntityData data)
        {
            return data switch
            {
                Document document => _dataProviderConnector.UpdateDataAsync(entityId, document),
                RichTextDocument richTextDocument => _dataProviderConnector.UpdateDataAsync(entityId, richTextDocument),
                MediaVideo mediaVideo => _dataProviderConnector.UpdateDataAsync(entityId, mediaVideo),
                Folder folder => _dataProviderConnector.UpdateDataAsync(entityId, folder),
                Space space => _dataProviderConnector.UpdateDataAsync(entityId, space),
                SysParameters sysParameters => _dataProviderConnector.UpdateDataAsync(entityId, sysParameters),
                _ => throw new InvalidOperationException(
                    $"No typed data-provider save path is defined for payload runtime type '{data.GetType().Name}'.")
            };
        }

        // Накладывает общие entity-метаданные поверх typed payload, полученного из data-provider.
        private static void ApplyEntityMetadata(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            data.Id = entityData.Id;
            data.Name = entityData.Name;
            data.CreatedDate = entityData.CreatedDate;
            data.LastModifiedDate = entityData.LastModifiedDate;
            data.EntityType = entityData.EntityType;

            if (string.IsNullOrWhiteSpace(data.Tag))
            {
                data.Tag = entityData.EntityType.ToString();
            }
        }
    }
}
