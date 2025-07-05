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
        private readonly object _lock = new object();
        private bool _seeded = false;

        public SampleDataService(
            BusinessEntityHelper helper,
            IPossibleEntityRelationTypesProvider relations)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _relations = relations ?? throw new ArgumentNullException(nameof(relations));
        }

        public async Task InitializeSampleDataAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_seeded) return;
                _seeded = true;
            }

            try
            {
                // Создаём главный Space
                var demoSpace = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Demo Space");

                // Получаем возможные типы отношений
                var relationTypes = _relations.GetPossibleRelations().ToList();
                var spaceContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-folder");
                var spaceContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-page");
                var folderContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-page");
                var folderContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-folder");

                // Создаём несколько страниц прямо в Space
                var directPage1 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, "Welcome Page");
                var directPage2 = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, "Quick Start Guide");
                
                if (spaceContainsPage != null)
                {
                    await _helper.CreateRelation(demoSpace, directPage1, spaceContainsPage, "");
                    await _helper.CreateRelation(demoSpace, directPage2, spaceContainsPage, "");
                }

                // Создаём 3 папки
                for (int i = 1; i <= 3; i++)
                {
                    var folder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Folder {i}");
                    
                    // Связываем Space с Folder
                    if (spaceContainsFolder != null)
                    {
                        await _helper.CreateRelation(demoSpace, folder, spaceContainsFolder, "");
                    }

                    // Создаём 2-3 страницы в каждой папке
                    int pageCount = i == 1 ? 2 : 3; // В первой папке 2 страницы, в остальных по 3
                    for (int j = 1; j <= pageCount; j++)
                    {
                        var page = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, $"Document {i}-{j}");
                        
                        // Связываем Folder с Page
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
                        var subPage = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, "Sub-document 1-1-1");
                        if (folderContainsPage != null)
                        {
                            await _helper.CreateRelation(subFolder, subPage, folderContainsPage, "");
                        }
                    }
                }

                // Демонстрация дублирования: добавляем одну из страниц также в другую папку
                var existingPage = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, "Shared Document");
                if (folderContainsPage != null)
                {
                    // Получаем первые две папки для демонстрации дублирования
                    var allFolders = await _helper.GetChildEntitiesAsync(demoSpace.Id);
                    var folders = allFolders.Where(e => e.EntityType == BusinessEntityTypeEnum.Folder).Take(2).ToList();
                    
                    foreach (var folder in folders)
                    {
                        await _helper.CreateRelation(folder, existingPage, folderContainsPage, "");
                    }
                }
            }
            catch (Exception)
            {
                // В случае ошибки сбрасываем флаг, чтобы можно было попробовать ещё раз
                lock (_lock)
                {
                    _seeded = false;
                }
                throw;
            }
        }
    }
} 