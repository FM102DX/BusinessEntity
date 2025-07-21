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
        private static readonly object _syncLock = new();
        private static bool _seededGlobal = false;

        public SampleDataService(
            BusinessEntityHelper helper,
            IPossibleEntityRelationTypesProvider relations)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _relations = relations ?? throw new ArgumentNullException(nameof(relations));
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
                // Проверим, есть ли уже пространства. Если есть, не создаём повторно.
                var existingEntities = await _helper.GetAllBusinessEntities();
                if (existingEntities.Any(e => e.EntityType == BusinessEntityTypeEnum.Space))
                {
                    return;
                }

                // Создаём два Space вместо одного
                var documentsSpace = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Документы");
                var newsSpace = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Новости");

                // Получаем возможные типы отношений
                var relationTypes = _relations.GetPossibleRelations().ToList();
                var spaceContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-folder");
                var spaceContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-page");
                var folderContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-page");
                var folderContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-folder");

                // Создаём несколько страниц прямо в пространстве "Документы"
                var directPage1 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Welcome Document");
                var directPage2 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Quick Start Guide");
                
                if (spaceContainsPage != null)
                {
                    await _helper.CreateRelation(documentsSpace, directPage1, spaceContainsPage, "");
                    await _helper.CreateRelation(documentsSpace, directPage2, spaceContainsPage, "");
                }

                // Создаём 3 папки в "Документы"
                for (int i = 1; i <= 3; i++)
                {
                    var folder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Folder {i}");
                    
                    // Связываем Space с Folder
                    if (spaceContainsFolder != null)
                    {
                        await _helper.CreateRelation(documentsSpace, folder, spaceContainsFolder, "");
                    }

                    // Создаём 2-3 страницы в каждой папке
                    int pageCount = i == 1 ? 2 : 3; // В первой папке 2 страницы, в остальных по 3
                    for (int j = 1; j <= pageCount; j++)
                    {
                        var page = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, $"Document {i}-{j}");
                        
                        // Связываем Folder с Document
                        if (folderContainsPage != null)
                        {
                            await _helper.CreateRelation(folder, page, folderContainsPage, "");
                        }
                    }
                    
                    // Создаём вложенную папку в первой папке для демонстрации
                    if (i == 1 && folderContainsFolder != null)
                    {
                        var subFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, "Subfolder 1-1");
                        await _helper.CreateRelation(folder, subFolder, folderContainsFolder, "");
                        
                        // Добавляем страницу в подпапку
                        var subPage = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Sub-document 1-1-1");
                        if (folderContainsPage != null)
                        {
                            await _helper.CreateRelation(subFolder, subPage, folderContainsPage, "");
                        }
                    }
                }

                // Демонстрация дублирования: добавляем одну из страниц также в другую папку
                var existingPage = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Shared Document");
                if (folderContainsPage != null)
                {
                    // Получаем первые две папки для демонстрации дублирования
                    var allFolders = await _helper.GetChildEntitiesAsync(documentsSpace.Id);
                    var folders = allFolders.Where(e => e.EntityType == BusinessEntityTypeEnum.Folder).Take(2).ToList();
                    
                    foreach (var folder in folders)
                    {
                        await _helper.CreateRelation(folder, existingPage, folderContainsPage, "");
                    }
                }

                // Заполняем пространство "Новости"
                // 1) Прямые страницы
                var newsDirect1 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Новости дня");
                var newsDirect2 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, "Аналитика");

                if (spaceContainsPage != null)
                {
                    await _helper.CreateRelation(newsSpace, newsDirect1, spaceContainsPage, "");
                    await _helper.CreateRelation(newsSpace, newsDirect2, spaceContainsPage, "");
                }

                // 2) Создаём 2 рубрики (папки) в "Новости"
                for (int i = 1; i <= 2; i++)
                {
                    var newsFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Рубрика {i}");

                    if (spaceContainsFolder != null)
                    {
                        await _helper.CreateRelation(newsSpace, newsFolder, spaceContainsFolder, "");
                    }

                    // В каждой рубрике по 2 новости
                    for (int j = 1; j <= 2; j++)
                    {
                        var newsArticle = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Document, $"Новость {i}-{j}");
                        if (folderContainsPage != null)
                        {
                            await _helper.CreateRelation(newsFolder, newsArticle, folderContainsPage, "");
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