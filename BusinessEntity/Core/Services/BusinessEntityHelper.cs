using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System.Linq;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.Core.Services
{
    /// <summary>
    /// Хелпер для работы с бизнес-сущностями
    /// </summary>
    public class BusinessEntityHelper
    {
        private readonly IDataProviderConnector _dataProviderConnector;
        private readonly IWebLoggerService? _webLogger;

        public BusinessEntityHelper(
            IDataProviderConnector dataProviderConnector,
            IWebLoggerService? webLogger)
        {
            _dataProviderConnector = dataProviderConnector ?? throw new ArgumentNullException(nameof(dataProviderConnector));
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

            return await _dataProviderConnector.AddAsync(entity);
        }

        public async Task RemoveBusinessEntity(Guid id)
        {
            _webLogger?.Warning($"RemoveBusinessEntity: id={id}");
            // Сначала удаляем все связи, где участвует данная сущность
            var relations = await _dataProviderConnector.GetAllAsync<Relation>(r => r.ObjectAId == id || r.ObjectBId == id);
            
            foreach (var relation in relations)
            {
                await _dataProviderConnector.DeleteAsync<Relation>(relation.Id);
            }

            // Затем удаляем саму сущность
            await _dataProviderConnector.DeleteAsync<Classes.BusinessEntity>(id);
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

            return await _dataProviderConnector.AddAsync(relation);
        }

        /// <summary>
        /// Удаляет все связи определенного типа между двумя сущностями
        /// </summary>
        /// <param name="entityA">Первая сущность</param>
        /// <param name="entityB">Вторая сущность</param>
        /// <param name="macroRelationType">Тип отношения для удаления</param>
        /// <returns>Количество удаленных связей</returns>
        public async Task<int> RemoveRelation(IBusinessEntity entityA, IBusinessEntity entityB, MacroRelationType macroRelationType)
        {
            _webLogger?.Information($"RemoveRelation: {entityA?.Name} -> {entityB?.Name} relation={macroRelationType?.RelationType}");
            if (entityA == null) throw new ArgumentNullException(nameof(entityA));
            if (entityB == null) throw new ArgumentNullException(nameof(entityB));
            if (macroRelationType == null) throw new ArgumentNullException(nameof(macroRelationType));

            // Находим все связи такого типа между сущностями А и Б
            var relationsToRemove = await _dataProviderConnector.GetAllAsync<Relation>(r => 
                r.ObjectAId == entityA.Id && 
                r.ObjectBId == entityB.Id && 
                r.RelationType == macroRelationType.RelationType.ToString());

            int removedCount = 0;
            foreach (var relation in relationsToRemove)
            {
                await _dataProviderConnector.DeleteAsync<Relation>(relation.Id);
                removedCount++;
                _webLogger?.Debug($"Removed relation: ID={relation.Id}, Type={relation.RelationType}");
            }

            _webLogger?.Information($"Removed {removedCount} relations of type {macroRelationType.RelationType} between {entityA.Name} and {entityB.Name}");
            return removedCount;
        }

        public async Task<Classes.BusinessEntity?> GetBusinessEntityById(Guid id)
        {
            _webLogger?.Debug($"GetBusinessEntityById: id={id}");
            return await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(id);
        }

        public async Task<Relation?> GetRelationById(Guid id)
        {
            return await _dataProviderConnector.GetByIdAsync<Relation>(id);
        }

        public async Task<IReadOnlyList<Classes.BusinessEntity>> GetAllBusinessEntities(int? take = null)
        {
            return await _dataProviderConnector.GetAllAsync<Classes.BusinessEntity>(null, take);
        }

        public async Task<IReadOnlyList<Classes.BusinessEntity>> GetSpacesAsync(int? take = null)
        {
            return await _dataProviderConnector.GetAllAsync<Classes.BusinessEntity>(
                e => e.EntityType == BusinessEntityTypeEnum.Space,
                take);
        }

        public async Task<IReadOnlyList<Relation>> GetAllRelations(int? take = null)
        {
            return await _dataProviderConnector.GetAllAsync<Relation>(null, take);
        }

        public async Task<IReadOnlyList<Relation>> GetRelationsByEntityIdAsync(Guid entityId, CancellationToken ct = default)
        {
            return await _dataProviderConnector.GetAllAsync<Relation>(r => r.ObjectAId == entityId || r.ObjectBId == entityId, cancellationToken: ct);
        }

        /// <summary>
        /// Получает все бизнес-сущности, содержащиеся в родительской сущности (визуально)
        /// </summary>
        public async Task<IEnumerable<Classes.BusinessEntity>> GetContainedEntitiesAsync(Guid parentId, CancellationToken ct = default)
        {
            var relations = await _dataProviderConnector.GetAllAsync<Relation>(r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString(), cancellationToken: ct);
            var childIds = relations.Select(r => r.ObjectBId).ToList();
            
            var children = new List<Classes.BusinessEntity>();
            foreach (var childId in childIds)
            {
                var child = await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(childId);
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
            // Находим все сущности, которые НЕ являются объектом B в отношении "VisuallyContains"
            var allEntities = await _dataProviderConnector.GetAllAsync<Classes.BusinessEntity>();
            var visuallyContainsRelations = await _dataProviderConnector.GetAllAsync<Relation>(r => r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString());
            var childIds = visuallyContainsRelations.Select(r => r.ObjectBId).ToHashSet();
            
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
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем связь между родителем и дочерним элементом (визуальная связь)
            var relation = new Relation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains.ToString(),
                RelationParams = ""
            };

            // Сохраняем связь
            await _dataProviderConnector.AddAsync(relation, cancellationToken: ct);

            _webLogger?.Information($"Created new folder '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }

        #region Document operations
        /// <summary>
        /// Создает новый документ внутри родительской папки или пространства
        /// </summary>
        public async Task<Classes.BusinessEntity> CreateDocumentAsync(
            Classes.BusinessEntity parent,
            CancellationToken ct = default)
        {
            // Backwards-compatible wrapper: create document with default body
            return await CreateDocumentAsync(parent, null, ct);
        }

        /// <summary>
        /// Создает новый документ с указанным текстом внутри родительской папки или пространства
        /// </summary>
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

            // Создаем документ как BusinessEntity с типом Document
            var entity = new Classes.BusinessEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                EntityType = BusinessEntityTypeEnum.Document,
                BusinessEntityType = BusinessEntityTypeEnum.Document
            };

            // Сохраняем сущность
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Создаем визуальную связь с родителем
            var relation = new Relation
            {
                Id = Guid.NewGuid(),
                ObjectAId = parent.Id,
                ObjectBId = entity.Id,
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains.ToString(),
                RelationParams = string.Empty
            };

            await _dataProviderConnector.AddAsync(relation, cancellationToken: ct);

            // Определяем текст для сохранения: используем переданный, иначе плейсхолдер из 100 символов
            var dataToSave = string.IsNullOrWhiteSpace(bodyText) ? new string('x', 100) : bodyText;

            // Создаем тело документа (BusinessEntityData)
            var beData = new BusinessEntityData
            {
                EntityId = entity.Id,
                Data = dataToSave!
            };

            await _dataProviderConnector.AddAsync(beData, cancellationToken: ct);

            _webLogger?.Debug($"Created BusinessEntityData for document '{name}' (DocID: {entity.Id}), DataLength={dataToSave?.Length ?? 0}");

            _webLogger?.Information($"Created new document '{name}' (ID: {entity.Id}) under parent '{parent.Name}' (ID: {parent.Id})");

            return entity;
        }
        #endregion

        /// <summary>
        /// Изменяет визуального родителя для элемента в визуальном дереве
        /// </summary>
        /// <param name="child">Дочерняя сущность, для которой меняется родитель</param>
        /// <param name="newVisualParent">Новый визуальный родитель</param>
        public async Task ChangeVisualFolderParentForItem(Classes.BusinessEntity child, Classes.BusinessEntity newVisualParent)
        {
            _webLogger?.Information($"ChangeVisualFolderParentForItem: Moving '{child?.Name}' to new visual parent '{newVisualParent?.Name}'");
            
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (newVisualParent == null) throw new ArgumentNullException(nameof(newVisualParent));

            // Проверяем, не создаст ли перемещение циклическую зависимость
            if (await WouldCreateCyclicDependency(child.Id, newVisualParent.Id))
            {
                var errorMessage = $"Cannot move '{child.Name}' to '{newVisualParent.Name}': this would create a cyclic dependency";
                _webLogger?.Warning(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Находим текущего визуального родителя данного элемента
            var currentVisualParentRelations = await _dataProviderConnector.GetAllAsync<Relation>(r => 
                r.ObjectBId == child.Id && 
                r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString());

            // Создаем MacroRelationType для VisuallyContains
            var visuallyContainsRelationType = new MacroRelationType
            {
                RelationType = BusinessEntityRelationTypeEnum.VisuallyContains
            };

            // Удаляем все существующие связи VisuallyContains для данного child
            foreach (var currentRelation in currentVisualParentRelations)
            {
                var currentParent = await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(currentRelation.ObjectAId);
                if (currentParent != null)
                {
                    _webLogger?.Debug($"Removing visual relation between '{currentParent.Name}' and '{child.Name}'");
                    await RemoveRelation(currentParent, child, visuallyContainsRelationType);
                }
            }

            // Создаем новую связь VisuallyContains между новым родителем и child
            await CreateRelation(newVisualParent, child, visuallyContainsRelationType);
            
            _webLogger?.Information($"Successfully moved '{child.Name}' to new visual parent '{newVisualParent.Name}'");
        }

        /// <summary>
        /// Перманентно удаляет бизнес-энтити из системы вместе с рекурсивным удалением всех дочерних элементов
        /// </summary>
        /// <param name="entityId">ID бизнес-энтити для удаления</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>Кортеж (bool успех, List&lt;string&gt; сообщения)</returns>
        public async Task<(bool success, List<string> messages)> RemoveBusinessEntityPermanently(Guid entityId, CancellationToken ct = default)
        {
            var messages = new List<string>();
            
            // Проверяем, существует ли сущность
            var entity = await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(entityId);
            if (entity == null)
            {
                // Если сущность не найдена, возвращаем true и пустой список (как указано в требованиях)
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
            await _dataProviderConnector.DeleteAsync<Classes.BusinessEntity>(entityId, ct);
            
            return (true, new List<string>());
        }
        
        /// <summary>
        /// Рекурсивно удаляет всех потомков бизнес-энтити в визуальном дереве
        /// </summary>
        /// <param name="parentId">ID родительской сущности</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>Кортеж (bool успех, List&lt;string&gt; сообщения)</returns>
        private async Task<(bool success, List<string> messages)> RemoveChildrenRecursively(Guid parentId, CancellationToken ct = default)
        {
            var allMessages = new List<string>();
            
            // Получаем всех прямых потомков (связи типа VisuallyContains, где parentId является ObjectA)
            var childRelations = await _dataProviderConnector.GetAllAsync<Relation>(
                r => r.ObjectAId == parentId && r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString(), 
                cancellationToken: ct);
            
            foreach (var relation in childRelations)
            {
                var childId = relation.ObjectBId;
                
                // Проверяем, существует ли дочерняя сущность
                var childEntity = await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(childId, cancellationToken: ct);
                if (childEntity == null)
                {
                    // Если дочерняя сущность не найдена, просто продолжаем
                    continue;
                }
                
                // Рекурсивно удаляем всех потомков этого ребенка
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
                await _dataProviderConnector.DeleteAsync<Classes.BusinessEntity>(childId, ct);
            }
            
            return (true, allMessages);
        }
        
        /// <summary>
        /// Проверяет, можно ли удалить бизнес-энтити (пока заглушка, всегда возвращает true)
        /// </summary>
        /// <param name="entityId">ID бизнес-энтити</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>true, если сущность можно удалить</returns>
        private async Task<bool> CanDelete(Guid entityId, CancellationToken ct = default)
        {
            // Заглушка - пока всегда возвращаем true
            // В будущем здесь будет логика проверки возможности удаления
            await Task.CompletedTask;
            return true;
        }
        
        /// <summary>
        /// Удаляет все связи бизнес-энтити (где сущность выступает как ObjectA или ObjectB)
        /// </summary>
        /// <param name="entityId">ID бизнес-энтити</param>
        /// <param name="ct">CancellationToken</param>
        private async Task RemoveAllEntityRelations(Guid entityId, CancellationToken ct = default)
        {
            // Получаем все связи, где сущность является ObjectA
            var relationsAsA = await _dataProviderConnector.GetAllAsync<Relation>(r => r.ObjectAId == entityId, cancellationToken: ct);
            
            // Получаем все связи, где сущность является ObjectB
            var relationsAsB = await _dataProviderConnector.GetAllAsync<Relation>(r => r.ObjectBId == entityId, cancellationToken: ct);
            
            // Удаляем все найденные связи
            foreach (var relation in relationsAsA.Concat(relationsAsB))
            {
                await _dataProviderConnector.DeleteAsync<Relation>(relation.Id, ct);
            }
        }

        /// <summary>
        /// Переименовывает бизнес-энтити
        /// </summary>
        /// <param name="entityId">ID сущности для переименования</param>
        /// <param name="newName">Новое имя сущности</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>Обновленная сущность или null, если сущность не найдена</returns>
        public async Task<Classes.BusinessEntity?> RenameEntity(Guid entityId, string newName, CancellationToken ct = default)
        {
            _webLogger?.Information($"RenameEntity: entityId={entityId}, newName='{newName}'");
            
            if (string.IsNullOrWhiteSpace(newName))
            {
                _webLogger?.Warning($"RenameEntity: новое имя не может быть пустым");
                return null;
            }
            
            // Получаем сущность
            var entity = await _dataProviderConnector.GetByIdAsync<Classes.BusinessEntity>(entityId, cancellationToken: ct);
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

        /// <summary>
        /// Проверяет, создаст ли перемещение элемента циклическую зависимость
        /// </summary>
        /// <param name="childId">ID элемента, который планируется переместить</param>
        /// <param name="newParentId">ID нового родителя</param>
        /// <returns>true, если создаст циклическую зависимость</returns>
        public async Task<bool> WouldCreateCyclicDependency(Guid childId, Guid newParentId)
        {
            // Если пытаемся переместить элемент в самого себя
            if (childId == newParentId)
                return true;

            // Проверяем, является ли newParent потомком child
            return await IsDescendantInDatabase(newParentId, childId);
        }

        /// <summary>
        /// Проверяет, является ли потенциальный потомок (descendantId) потомком предка (ancestorId) в базе данных
        /// </summary>
        /// <param name="descendantId">ID потенциального потомка</param>
        /// <param name="ancestorId">ID предка</param>
        /// <returns>true, если descendant является потомком ancestor</returns>
        private async Task<bool> IsDescendantInDatabase(Guid descendantId, Guid ancestorId)
        {
            // Получаем родителя descendantId
            var parentRelations = await _dataProviderConnector.GetAllAsync<Relation>(r => 
                r.ObjectBId == descendantId && 
                r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString());

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

        public async Task<IReadOnlyList<BusinessEntityData>> GetData(Classes.BusinessEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _webLogger?.Debug($"GetData: entityId={entity.Id}, type={entity.EntityType}");

            // Базовая загрузка всех чанков по EntityId
            var chunks = await _dataProviderConnector.GetAllAsync<BusinessEntityData>(d => d.EntityId == entity.Id);

            // Возможность различать логику по типам сущностей
            switch (entity.EntityType)
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

        /// <summary>
        /// Сохраняет бизнес-энтити и связанный с ней блок данных
        /// </summary>
        /// <param name="entity">Сущность (например, документ)</param>
        /// <param name="data">Данные сущности</param>
        public async Task SaveEntity(Classes.BusinessEntity entity, BusinessEntityData data)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Всегда удостоверяемся, что данные привязаны к текущей сущности
            data.EntityId = entity.Id;

            _webLogger?.Information($"SaveEntity: entityId={entity.Id}, name='{entity.Name}', dataLen={data?.Data?.Length ?? 0}");

            // Сохраняем (добавляем или обновляем) саму сущность
            if (await _dataProviderConnector.ExistsAsync<Classes.BusinessEntity>(entity.Id))
            {
                await _dataProviderConnector.UpdateAsync(entity);
            }
            else
            {
                await _dataProviderConnector.AddAsync(entity);
            }

            // Сохраняем (добавляем или обновляем) данные сущности
            if (await _dataProviderConnector.ExistsAsync<BusinessEntityData>(data.Id))
            {
                await _dataProviderConnector.UpdateAsync(data);
            }
            else
            {
                await _dataProviderConnector.AddAsync(data);
            }

            _webLogger?.Debug($"SaveEntity: saved entity {entity.Id} and data {data.Id}");
        }
    }
}
