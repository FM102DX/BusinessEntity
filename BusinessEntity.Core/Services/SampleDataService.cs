using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Core.Services
{
    public class SampleDataService : ISampleDataService
    {
        private readonly BusinessEntityHelper _helper;
        private readonly IPossibleEntityRelationTypesProvider _relations;
        private readonly IDataFillLineProvider _dataFill;
        private readonly IWebLoggerService? _webLogger;
        private static readonly object _syncLock = new();
        private static bool _seededGlobal = false;

        public SampleDataService(
            BusinessEntityHelper helper,
            IPossibleEntityRelationTypesProvider relations,
            IDataFillLineProvider dataFill,
            IWebLoggerService? webLogger)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _relations = relations ?? throw new ArgumentNullException(nameof(relations));
            _dataFill = dataFill ?? throw new ArgumentNullException(nameof(dataFill));
            _webLogger = webLogger;
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
                _webLogger?.Information("[Seed] InitializeSampleDataAsync: start");
                // Проверяем наличие пространств и связей для дерева.
                var existingEntities = await _helper.GetAllBusinessEntities();
                var existingSpaces = existingEntities.Where(e => e.EntityType == BusinessEntityTypeEnum.Space).ToList();

                // Если пространства существуют И при этом уже есть хотя бы одна связь VisuallyContains –
                // считаем, что демонстрационные данные уже были сгенерированы и можно выйти.
                var existingRelations = await _helper.GetAllRelations();
                bool hasVisualRelations = existingRelations.Any(r => r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString());
                _webLogger?.Information($"[Seed] Existing: entities={existingEntities.Count}, spaces={existingSpaces.Count}, relations={existingRelations.Count}, visualRelations={existingRelations.Count(r => r.RelationType == BusinessEntityRelationTypeEnum.VisuallyContains.ToString())}");
                if (existingSpaces.Any() && hasVisualRelations)
                {
                    _webLogger?.Information("[Seed] Skip: spaces already exist and visual relations present");
                    return;
                }

                // Если либо нет пространств, либо отсутствуют связи для дерева – генерируем демо-данные повторно.

                // Находим или создаём пространства (idempotent)
                var docsExisted = existingSpaces.Any(s => s.Name == "Документы");
                var documentsSpace = existingSpaces.FirstOrDefault(s => s.Name == "Документы")
                                     ?? await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Документы");
                _webLogger?.Information($"[Seed] Space 'Документы': {(docsExisted ? "reused" : "created")}, Id={documentsSpace.Id}");
                var newsExisted = existingSpaces.Any(s => s.Name == "Новости");
                var newsSpace = existingSpaces.FirstOrDefault(s => s.Name == "Новости")
                                     ?? await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, "Новости");
                _webLogger?.Information($"[Seed] Space 'Новости': {(newsExisted ? "reused" : "created")}, Id={newsSpace.Id}");

                // Получаем возможные типы отношений
                var relationTypes = _relations.GetPossibleRelations().ToList();
                var spaceContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-folder");
                var spaceContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:space-contains-page");
                var folderContainsPage = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-page");
                var folderContainsFolder = relationTypes.FirstOrDefault(r => r.RelationName == "basic:folder-contains-folder");
                _webLogger?.Information($"[Seed] RelationTypes: spaceContainsFolder={(spaceContainsFolder != null)}, spaceContainsPage={(spaceContainsPage != null)}, folderContainsPage={(folderContainsPage != null)}, folderContainsFolder={(folderContainsFolder != null)}");

                // Проверяем, есть ли уже дети у пространств (чтобы не дублировать наполнение)
                var docsHasChildren = (await _helper.GetContainedEntitiesAsync(documentsSpace.Id, ct)).Any();
                var newsHasChildren = (await _helper.GetContainedEntitiesAsync(newsSpace.Id, ct)).Any();
                _webLogger?.Information($"[Seed] Children flags: docsHasChildren={docsHasChildren}, newsHasChildren={newsHasChildren}");

                if (!docsHasChildren)
                {
                    // Создаём несколько страниц прямо в пространстве "Документы" c заполнением текста
                    try
                    {
                        _webLogger?.Information("[Seed] Creating direct pages in 'Документы' space");
                        var directPage1Text = await _dataFill.GetNextLineAsync(ct);
                        var directPage1 = await _helper.CreateDocumentAsync(documentsSpace, directPage1Text, ct);
                        await _helper.RenameEntity(directPage1.Id, "Welcome Document", ct);
                        _webLogger?.Information($"[Seed] Created direct document 1: Id={directPage1.Id}");

                        var directPage2Text = await _dataFill.GetNextLineAsync(ct);
                        var directPage2 = await _helper.CreateDocumentAsync(documentsSpace, directPage2Text, ct);
                        await _helper.RenameEntity(directPage2.Id, "Quick Start Guide", ct);
                        _webLogger?.Information($"[Seed] Created direct document 2: Id={directPage2.Id}");
                    }
                    catch (Exception ex)
                    {
                        _webLogger?.Error(FormatException(ex, "Creating direct pages in 'Документы'"));
                        _webLogger?.Warning("[Seed] Error while creating direct pages in 'Документы'");
                        throw;
                    }

                    // Создаём 3 папки в "Документы"
                    for (int i = 1; i <= 3; i++)
                    {
                        try
                        {
                            _webLogger?.Information($"[Seed] Creating folder {i} in 'Документы'");
                            var folder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Folder {i}");
                            _webLogger?.Information($"[Seed] Created folder {i}: Id={folder.Id}");
                            
                            // Связываем Space с Folder
                            if (spaceContainsFolder != null)
                            {
                                await _helper.CreateRelation(documentsSpace, folder, spaceContainsFolder, "");
                                _webLogger?.Information($"[Seed] Linked Space -> Folder {i} via {spaceContainsFolder.RelationType}");
                            }
                            else
                            {
                                _webLogger?.Warning("[Seed] Relation type 'space-contains-folder' not found");
                            }

                            // Создаём 2-3 страницы в каждой папке (с наполнением текста)
                            int pageCount = i == 1 ? 2 : 3; // В первой папке 2 страницы, в остальных по 3
                            for (int j = 1; j <= pageCount; j++)
                            {
                                var pageText = await _dataFill.GetNextLineAsync(ct);
                                var page = await _helper.CreateDocumentAsync(folder, pageText, ct);
                                await _helper.RenameEntity(page.Id, $"Document {i}-{j}", ct);
                                _webLogger?.Information($"[Seed] Created document {i}-{j}: Id={page.Id}");
                            }
                            
                            // Создаём вложенную папку в первой папке для демонстрации
                            if (i == 1 && folderContainsFolder != null)
                            {
                                var subFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, "Subfolder 1-1");
                                await _helper.CreateRelation(folder, subFolder, folderContainsFolder, "");
                                _webLogger?.Information($"[Seed] Created subfolder under Folder 1: Id={subFolder.Id}");
                                
                                // Добавляем страницу в подпапку (с наполнением текста)
                                var subText = await _dataFill.GetNextLineAsync(ct);
                                var subPage = await _helper.CreateDocumentAsync(subFolder, subText, ct);
                                await _helper.RenameEntity(subPage.Id, "Sub-document 1-1-1", ct);
                                _webLogger?.Information($"[Seed] Created sub document under Subfolder 1-1: Id={subPage.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _webLogger?.Error(FormatException(ex, $"Creating folder {i} and its contents in 'Документы'"));
                            _webLogger?.Warning($"[Seed] Error while creating folder {i} and its contents");
                            throw;
                        }
                    }
                }
                // Демонстрация дублирования удалена: каждый документ находится ровно в одной папке

                if (!newsHasChildren)
                {
                    // Заполняем пространство "Новости"
                    // 1) Прямые страницы
                    try
                    {
                        _webLogger?.Information("[Seed] Creating direct pages in 'Новости' space");
                        var newsDirect1Text = await _dataFill.GetNextLineAsync(ct);
                        var newsDirect1 = await _helper.CreateDocumentAsync(newsSpace, newsDirect1Text, ct);
                        await _helper.RenameEntity(newsDirect1.Id, "Новости дня", ct);
                        _webLogger?.Information($"[Seed] Created 'Новости дня' Id={newsDirect1.Id}");
                        var newsDirect2Text = await _dataFill.GetNextLineAsync(ct);
                        var newsDirect2 = await _helper.CreateDocumentAsync(newsSpace, newsDirect2Text, ct);
                        await _helper.RenameEntity(newsDirect2.Id, "Аналитика", ct);
                        _webLogger?.Information($"[Seed] Created 'Аналитика' Id={newsDirect2.Id}");
                    }
                    catch (Exception ex)
                    {
                        _webLogger?.Error(FormatException(ex, "Creating direct pages in 'Новости'"));
                        _webLogger?.Warning("[Seed] Error while creating direct pages in 'Новости'");
                        throw;
                    }

                    // 2) Создаём 2 рубрики (папки) в "Новости"
                    for (int i = 1; i <= 2; i++)
                    {
                        try
                        {
                            _webLogger?.Information($"[Seed] Creating 'Рубрика {i}' in 'Новости'");
                            var newsFolder = await _helper.CreateBusinessEntity(BusinessEntityTypeEnum.Folder, $"Рубрика {i}");
                            if (spaceContainsFolder != null)
                            {
                                await _helper.CreateRelation(newsSpace, newsFolder, spaceContainsFolder, "");
                                _webLogger?.Information($"[Seed] Linked 'Новости' -> 'Рубрика {i}' via {spaceContainsFolder.RelationType}");
                            }
                            else
                            {
                                _webLogger?.Warning("[Seed] Relation type 'space-contains-folder' not found (Новости)");
                            }

                            // В каждой рубрике по 2 новости (с наполнением текста)
                            for (int j = 1; j <= 2; j++)
                            {
                                var newsText = await _dataFill.GetNextLineAsync(ct);
                                var newsArticle = await _helper.CreateDocumentAsync(newsFolder, newsText, ct);
                                await _helper.RenameEntity(newsArticle.Id, $"Новость {i}-{j}", ct);
                                _webLogger?.Information($"[Seed] Created 'Новость {i}-{j}' Id={newsArticle.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _webLogger?.Error(FormatException(ex, $"Creating 'Рубрика {i}' and its news in 'Новости'"));
                            _webLogger?.Warning($"[Seed] Error while creating 'Рубрика {i}' and its news");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки сбрасываем флаг, чтобы можно было попробовать ещё раз
                try
                {
                    _webLogger?.Error(FormatException(ex, "InitializeSampleDataAsync"));
                    _webLogger?.Warning("[Seed] Seeding failed. Resetting seeded flag.");
                }
                catch { }
                lock (_syncLock)
                {
                    _seededGlobal = false;
                }
                throw;
            }
        }

        private static string FormatException(Exception ex, string? context = null)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine($"[Seed] Exception context: {context}");
            }
            int level = 0;
            var current = ex;
            while (current != null)
            {
                var prefix = level == 0 ? "EX" : $"INNER[{level}]";
                sb.AppendLine($"{prefix}: {current.GetType().FullName} HResult=0x{current.HResult:X8}");
                sb.AppendLine($"Message: {current.Message}");
                if (!string.IsNullOrWhiteSpace(current.Source)) sb.AppendLine($"Source: {current.Source}");
                if (current.TargetSite != null) sb.AppendLine($"TargetSite: {current.TargetSite}");
                // Dump Data dictionary if present
                if (current.Data != null && current.Data.Count > 0)
                {
                    sb.AppendLine("Data:");
                    foreach (System.Collections.DictionaryEntry entry in current.Data)
                    {
                        sb.AppendLine($"  {entry.Key} = {entry.Value}");
                    }
                }
                sb.AppendLine("StackTrace:");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine("----");
                current = current.InnerException;
                level++;
            }
            return sb.ToString();
        }
    }
} 