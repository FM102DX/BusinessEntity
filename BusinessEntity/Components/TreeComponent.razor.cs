using Microsoft.AspNetCore.Components;
using BusinessEntity.Models;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Components
{
    public partial class TreeComponent : ComponentBase
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public ILogger<TreeComponent> Logger { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }

        [Parameter] public EventCallback<TreeNodeItemViewModelBase> OnNodeSelected { get; set; }
        public List<Core.Classes.BusinessEntity>? AllEntities { get; set; }
        private IEnumerable<TreeNodeItemViewModelBase> TreeData { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsLoading { get; set; } = true;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                IsLoading = true;
                
                // Инициализируем демо-данные если их нет
                await SampleDataService.InitializeSampleDataAsync();
                
                // Строим дерево
                TreeData = await BuildTreeAsync();
                
                Logger.LogInformation("TreeComponent initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing TreeComponent");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<IEnumerable<TreeNodeItemViewModelBase>> BuildTreeAsync()
        {
            try
            {
                await WebLogger.Information("Building tree");
                var allBusinessEntities = await BusinessEntityHelper.GetAllBusinessEntities();
                var strAllEntDump = string.Join($"{string.Empty}", allBusinessEntities.ToList().Select(x => x.Name));
                await WebLogger.Information(strAllEntDump);


                AllEntities = allBusinessEntities.ToList();
                var rootEntities = AllEntities.Where(x => x.EntityType == BusinessEntityTypeEnum.Space);
                var treeNodes = new List<TreeNodeItemViewModelBase>();
                foreach (var entity in rootEntities)
                {
                    var treeNode = await BuildTreeNodeAsync(entity);
                    treeNodes.Add(treeNode);
                }

                return treeNodes;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error building tree");
                return new List<TreeNodeItemViewModelBase>();
            }
        }

        private async Task<TreeNodeItemViewModelBase> BuildTreeNodeAsync(BusinessEntity.Core.Classes.BusinessEntity entity)
        {
            var icon = GetEntityIcon(entity.EntityType);
            var treeNodeVm = new TreeNodeItemViewModelBase
            {
                Title = entity.Name,
                Icon = icon,
                Entity = entity,
                EntityType = entity.EntityType.ToString(),
                Expanded = true
            };

            // Получаем дочерние сущности
            var children = await BusinessEntityHelper.GetChildEntitiesAsync(entity.Id);
            var childNodes = new List<TreeNodeItemViewModelBase>();

            foreach (var child in children)
            {
                var childNode = await BuildTreeNodeAsync(child);
                childNodes.Add(childNode);
            }

            treeNodeVm.Children = childNodes;
            return treeNodeVm;
        }

        private string GetEntityIcon(BusinessEntityTypeEnum entityType)
        {
            return entityType switch
            {
                BusinessEntityTypeEnum.Space => "📁",
                BusinessEntityTypeEnum.Folder => "📂",
                BusinessEntityTypeEnum.Page => "📄",
                _ => "❓"
            };
        }

        private string GetNodeText(object data)
        {
            if (data is TreeNodeItemViewModelBase node)
            {
                return $"{GetEntityIcon(node.Entity?.EntityType ?? BusinessEntityTypeEnum.Space)} {node.Title}";
            }
            return "Unknown";
        }

        public async Task RefreshTreeAsync()
        {
            IsLoading = true;
            StateHasChanged();
            
            try
            {
                TreeData = await BuildTreeAsync();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        // Метод для создания дополнительных тестовых данных
        public async Task CreateAdditionalTestDataAsync()
        {
            try
            {
                // Получаем корневые сущности
                var rootEntities = await BusinessEntityHelper.GetRootEntitiesAsync();
                var demoSpace = rootEntities.FirstOrDefault(e => e.EntityType == BusinessEntityTypeEnum.Space);
                
                if (demoSpace != null)
                {
                    // Создаем дополнительную страницу прямо в Space
                    var directPage = await BusinessEntityHelper.CreateBusinessEntity(BusinessEntityTypeEnum.Page, "Direct Page in Space");
                    
                    // Получаем типы отношений
                    var relationTypes = BusinessEntityHelper.GetType().Assembly
                        .GetTypes()
                        .Where(t => t.GetInterfaces().Contains(typeof(IPossibleEntityRelationTypesProvider)))
                        .FirstOrDefault();
                    
                    if (relationTypes != null)
                    {
                        var provider = Activator.CreateInstance(relationTypes) as IPossibleEntityRelationTypesProvider;
                        var spaceContainsPage = provider?.GetPossibleRelations()
                            .FirstOrDefault(r => r.RelationName == "basic:space-contains-page");
                        
                        if (spaceContainsPage != null)
                        {
                            await BusinessEntityHelper.CreateRelation(demoSpace, directPage, spaceContainsPage, "");
                        }
                    }
                }
                
                Logger.LogInformation("Additional test data created successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating additional test data");
            }
        }

        public async Task CreateAdditionalTestDataAndRefreshAsync()
        {
            IsLoading = true;
            StateHasChanged();
            
            try
            {
                await CreateAdditionalTestDataAsync();
                TreeData = await BuildTreeAsync();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }
    }
} 