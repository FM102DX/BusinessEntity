using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.Services
{
    public class SampleDataService : ISampleDataService
    {
        private readonly BusinessEntityHelper _helper;
        private readonly IPossibleEntityRelationTypesProvider _relations;
        private readonly IDataFillLineProvider _dataFill;
        private static readonly object _syncLock = new();
        private static bool _seededGlobal = false;

        public SampleDataService(
            BusinessEntityHelper helper,
            IPossibleEntityRelationTypesProvider relations,
            IDataFillLineProvider dataFill)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _relations = relations ?? throw new ArgumentNullException(nameof(relations));
            _dataFill = dataFill ?? throw new ArgumentNullException(nameof(dataFill));
        }

        public async Task InitializeSampleDataAsync(CancellationToken ct = default)
        {
            lock (_syncLock)
            {
                if (_seededGlobal) return;
                _seededGlobal = true;
            }

            try
            {
                // Проверяем наличие пространств и связей для дерева.
                var existingEntities = await _helper.GetAllBusinessEntities();
                var existingSpaces = existingEntities.Where(e => e.EntityType == BusinessEntityTypeEnum.Space).ToList();

                // Если пространства существуют И при этом уже есть хотя бы одна связь VisuallyContains –
                // считаем, что демонстрационные данные уже были сгенерированы и можно выйти.
                var existingRelations = await _helper.GetAllRelations();
                bool hasVisualRelations = existingRelations.Any(r => r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString());
                if (existingSpaces.Any() && hasVisualRelations)
                {
                    return;
                }

                // Если либо нет пространств, либо отсутствуют связи для дерева – генерируем демо-данные повторно.

                // Находим или создаём пространства (idempotent)
                var documentsSpace = existingSpaces.FirstOrDefault(s => s.Name == "Документы")
                                     ?? await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Документы");
                var newsSpace = existingSpaces.FirstOrDefault(s => s.Name == "Новости")
                                     ?? await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Новости");

                // Получаем возможные типы отношений
                var relationTypes = _relations.GetPossibleRelations().ToList();
                var spaceContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-folder");
                var spaceContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-page");
                var folderContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-page");
                var folderContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-folder");

                // Проверяем, есть ли уже дети у пространств (чтобы не дублировать наполнение)
                var docsHasChildren = (await _helper.GetContainedEntitiesAsync(documentsSpace.Id, ct)).Any();
                var newsHasChildren = (await _helper.GetContainedEntitiesAsync(newsSpace.Id, ct)).Any();

                if (!docsHasChildren)
                {
                    // Создаём несколько страниц прямо в пространстве "Документы" c заполнением текста
                    var directPage1Text = await _dataFill.GetNextLineAsync(ct);
                    var directPage1 = await _helper.CreateDocumentAsync(documentsSpace, directPage1Text, ct);
                    await _helper.RenameEntity(directPage1.Id, "Welcome Document", ct);

                    var directPage2Text = await _dataFill.GetNextLineAsync(ct);
                    var directPage2 = await _helper.CreateDocumentAsync(documentsSpace, directPage2Text, ct);
                    await _helper.RenameEntity(directPage2.Id, "Quick Start Guide", ct);

                    // Создаём 3 папки в "Документы"
                    for (int i = 1; i <= 3; i++)
                    {
                        var folder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Folder {i}");
                        
                        // Связываем Space с Folder
                        if (spaceContainsFolder != null)
                        {
                            await _helper.CreateRelation(documentsSpace, folder, spaceContainsFolder, "");
                        }

                        // Создаём 2-3 страницы в каждой папке (с наполнением текста)
                        int pageCount = i == 1 ? 2 : 3; // В первой папке 2 страницы, в остальных по 3
                        for (int j = 1; j <= pageCount; j++)
                        {
                            var pageText = await _dataFill.GetNextLineAsync(ct);
                            var page = await _helper.CreateDocumentAsync(folder, pageText, ct);
                            await _helper.RenameEntity(page.Id, $"Document {i}-{j}", ct);
                        }
                        
                        // Создаём вложенную папку в первой папке для демонстрации
                        if (i == 1 && folderContainsFolder != null)
                        {
                            var subFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, "Subfolder 1-1");
                            await _helper.CreateRelation(folder, subFolder, folderContainsFolder, "");
                            
                            // Добавляем страницу в подпапку (с наполнением текста)
                            var subText = await _dataFill.GetNextLineAsync(ct);
                            var subPage = await _helper.CreateDocumentAsync(subFolder, subText, ct);
                            await _helper.RenameEntity(subPage.Id, "Sub-document 1-1-1", ct);
                        }
                    }
                }
                // Демонстрация дублирования удалена: каждый документ находится ровно в одной папке

                if (!newsHasChildren)
                {
                    // Заполняем пространство "Новости"
                    // 1) Прямые страницы
                    var newsDirect1Text = await _dataFill.GetNextLineAsync(ct);
                    var newsDirect1 = await _helper.CreateDocumentAsync(newsSpace, newsDirect1Text, ct);
                    await _helper.RenameEntity(newsDirect1.Id, "Новости дня", ct);
                    var newsDirect2Text = await _dataFill.GetNextLineAsync(ct);
                    var newsDirect2 = await _helper.CreateDocumentAsync(newsSpace, newsDirect2Text, ct);
                    await _helper.RenameEntity(newsDirect2.Id, "Аналитика", ct);

                    // 2) Создаём 2 рубрики (папки) в "Новости"
                    for (int i = 1; i <= 2; i++)
                    {
                        var newsFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Рубрика {i}");

                        if (spaceContainsFolder != null)
                        {
                            await _helper.CreateRelation(newsSpace, newsFolder, spaceContainsFolder, "");
                        }

                        // В каждой рубрике по 2 новости (с наполнением текста)
                        for (int j = 1; j <= 2; j++)
                        {
                            var newsText = await _dataFill.GetNextLineAsync(ct);
                            var newsArticle = await _helper.CreateDocumentAsync(newsFolder, newsText, ct);
                            await _helper.RenameEntity(newsArticle.Id, $"Новость {i}-{j}", ct);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // В случае ошибки сбрасываем флаг, чтобы можно было попробовать ещё раз
                lock (_syncLock)
                {
                    _seededGlobal = false;
                }
                throw;
            }
        }
    }
} 