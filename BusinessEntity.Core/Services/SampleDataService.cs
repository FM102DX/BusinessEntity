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
                var folderContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-page");

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