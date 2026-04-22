using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.DomainEntities;
using System.Linq;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.WebLogger.Services;

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

        // Возвращает дочерние entity по визуальной связи
        public async Task<IEnumerable<Classes.BusinessEntity>> GetContainedEntitiesAsync(Guid parentId, CancellationToken ct = default)
        {
            // Ищем relation вида родитель -> ребенок
            var relations = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())
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

        // Возвращает корневые entity, у которых нет визуального родителя
        public async Task<IEnumerable<Classes.BusinessEntity>> GetRootEntitiesAsync()
        {
            // Находим все сущности, которые НЕ являются объектом B в отношении "VisuallyContains"
            var allEntities = await _dataProviderConnector.GetAllAsync();
            var visuallyContainsRelations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r => r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())
                .ToList();
            var childIds = visuallyContainsRelations.Select(r => r.ObjectBId).ToHashSet();
            
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
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем визуальную связь родитель -> дочерний элемент
            var relation = new BusinessEntityRelation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains.ToString(),
                RelationParams = ""
            };

            // Сохраняем связь
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

            // Создаем runtime-объект документа через фабрику
            var entity = CreateEntityForType(BusinessEntityTypeEnum.Document, name, dataToSave);

            // Сохраняем сущность
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем визуальную связь с родителем
            var relation = new BusinessEntityRelation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains.ToString(),
                RelationParams = string.Empty
            };

            await _dataProviderConnector.CreateRelationAsync(relation, cancellationToken: ct);

            // Создаем payload документа
            await _dataProviderConnector.UpdateDataAsync(entity.Id, dataToSave!, ct);

            _webLogger?.Debug($"Created BusinessEntityData for document '{name}' (DocID: {entity.Id}), DataLength={dataToSave?.Length ?? 0}");

            _webLogger?.Information($"Created new document '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }
        #endregion

        // Меняет визуального родителя элемента в дереве
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

            // Находим текущих визуальных родителей элемента
            var currentVisualParentRelations = (await _dataProviderConnector.GetAllRelationsAsync())
                .Where(r =>
                    r.ObjectBId == child.Id &&
                    r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())
                .ToList();

            // Создаем макро-описание нужной связи
            var visuallyContainsRelationType = new MacroRelationType
            {
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains
            };

            // Удаляем старые визуальные связи
            foreach (var currentRelation in currentVisualParentRelations)
            {
                var currentParent = await _dataProviderConnector.GetByIdAsync(currentRelation.ObjectAId);
                if (currentParent != null)
                {
                    _webLogger?.Debug($"Removing visual relation between '{currentParent.Name}' and '{child.Name}'");
                    await RemoveRelation(currentParent, child, visuallyContainsRelationType);
                }
            }

            // Создаем новую визуальную связь
            await CreateRelation(newVisualParent, child, visuallyContainsRelationType);
            
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
        
        // Рекурсивно удаляет всех визуальных потомков сущности
        private async Task<(bool success, List<string> messages)> RemoveChildrenRecursively(Guid parentId, CancellationToken ct = default)
        {
            var allMessages = new List<string>();
            
            // Получаем всех прямых потомков по визуальной связи
            var childRelations = (await _dataProviderConnector.GetAllRelationsAsync(ct))
                .Where(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())
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
                    r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())
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

            // Базовая загрузка сырых данных по Id
            var rawData = await _dataProviderConnector.GetDataAsync<string>(entityData.Id);
            var chunks = string.IsNullOrEmpty(rawData)
                ? Array.Empty<BusinessEntityData>()
                : new[] { CreateDataObject(entityData, rawData) };

            // Здесь можно расширять типовую пост-обработку
            switch (entityData.EntityType)
            {
                case BusinessEntityTypeEnum.Space:
                    // Специфическая обработка для Space (при необходимости)
                    break;
                case BusinessEntityTypeEnum.Folder:
                    // Специфическая обработка для Folder
                    break;
                case BusinessEntityTypeEnum.Document:
                    // Специфическая обработка для Document
                    break;
                default:
                    // По умолчанию — без дополнительной обработки
                    break;
            }

            return chunks;
        }

        // Сохраняет entity и ее payload в хранилище
        public async Task SaveEntity(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            if (entityData == null) throw new ArgumentNullException(nameof(entityData));
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Всегда выравниваем identity и тип перед сохранением
            data.Id = entityData.Id;
            data.EntityType = entityData.EntityType;

            // Извлекаем сериализуемый payload
            var payload = ExtractPayload(data);
            var entityToSave = CreatePersistenceEntity(entityData, data);

            _webLogger?.Information($"SaveEntity: entityId={entityData.Id}, name='{entityData.Name}', dataLen={payload?.Length ?? 0}");

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

            // Сохраняем payload сущности отдельно
            await _dataProviderConnector.UpdateDataAsync(entityToSave.Id, payload);

            _webLogger?.Debug($"SaveEntity: saved entityData {entityData.Id} and data {data.Id}");
        }

        // Выделяет строковый payload из data-объекта
        private static string ExtractPayload(BusinessEntityData data)
        {
            return data switch
            {
                Document document => document.Text ?? string.Empty,
                _ => string.Empty
            };
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
                _ => _businessEntityFactory.Create(type, name)
            };
        }

        // Создает data-объект для runtime-работы по типу entity
        private BusinessEntityData CreateDataObject(Classes.BusinessEntity entityData, string rawData)
        {
            if (entityData.EntityType == BusinessEntityTypeEnum.Document)
            {
                var typedEntity = _businessEntityFactory.Create(
                    BusinessEntityTypeEnum.Document,
                    new Document
                    {
                        Name = entityData.Name,
                        Text = rawData
                    },
                    entityData.Name);

                typedEntity.Id = entityData.Id;
                typedEntity.CreatedDate = entityData.CreatedDate;
                typedEntity.LastModifiedDate = entityData.LastModifiedDate;
                typedEntity.Name = entityData.Name;
                typedEntity.BusinessEntityType = entityData.BusinessEntityType;
                typedEntity.EntityType = entityData.EntityType;
                typedEntity.SynchronizeDataWithEntity();
                typedEntity.Data.Text = rawData;

                return typedEntity.Data;
            }

            return new BusinessEntityData
            {
                Id = entityData.Id,
                Name = entityData.Name,
                CreatedDate = entityData.CreatedDate,
                LastModifiedDate = entityData.LastModifiedDate,
                EntityType = entityData.EntityType
            };
        }

        // Подготавливает entity к сохранению через connector
        private Classes.BusinessEntity CreatePersistenceEntity(Classes.BusinessEntity entityData, BusinessEntityData data)
        {
            var entityType = ResolveEntityType(entityData, data);

            return data switch
            {
                Document document => CreateDocumentPersistenceEntity(entityData, document, entityType),
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
                    Text = document.Text ?? string.Empty
                },
                entityData.Name);

            typedEntity = CopyEntityState(entityData, typedEntity);
            typedEntity.Data.Tag = document.Tag;
            typedEntity.Data.Text = document.Text ?? string.Empty;

            return typedEntity;
        }

        // Копирует метаданные существующей entity в новую runtime-entity
        private static Classes.BusinessEntity CopyEntityState(Classes.BusinessEntity source, Classes.BusinessEntity target)
        {
            target.Id = source.Id;
            target.CreatedDate = source.CreatedDate;
            target.LastModifiedDate = source.LastModifiedDate;
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
    }
}
