using Microsoft.AspNetCore.Components;
using BusinessEntity.Models;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;
using SampleOnlineMall.WebLogger.Services;
using System.Linq;
using BusinessEntity.Contracts;

namespace BusinessEntity.Components
{
    public partial class TreeComponent : ComponentBase
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public ILogger<TreeComponent> Logger { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }
        [Inject] public IUserContextService UserContext { get; set; } = default!;

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
                AllEntities = allBusinessEntities.ToList();

                var treeNodes = new List<TreeNodeItemViewModelBase>();

                if (UserContext.HasSelectedSpace)
                {
                    var currentSpace = AllEntities.FirstOrDefault(e => e.Id == UserContext.CurrentSpaceId);
                    if (currentSpace != null)
                    {
                        var children = await BusinessEntityHelper.GetChildEntitiesAsync(currentSpace.Id);
                        foreach (var child in children)
                        {
                            var node = await BuildTreeNodeAsync(child);
                            treeNodes.Add(node);
                        }
                    }
                }
                else
                {
                    // Fallback: если пространство не выбрано (теоретически не должно быть) – покажем все пространства
                    var rootSpaces = AllEntities.Where(x => x.EntityType == BusinessEntityTypeEnum.Space);
                    foreach (var space in rootSpaces)
                    {
                        var node = await BuildTreeNodeAsync(space);
                        treeNodes.Add(node);
                    }
                }

                // Dump
                if (WebLogger != null)
                {
                    var treeDumpText = DumpTree(treeNodes, 0);
                    await WebLogger.Information($"Tree dump:\n{treeDumpText}");
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

        private string DumpTree(IEnumerable<TreeNodeItemViewModelBase> nodes, int indentLevel)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var node in nodes)
            {
                var indent = new string('-', indentLevel * 2);
                sb.AppendLine($"{indent}{node.Icon} {node.Title}");

                if (node.Children != null && node.Children.Any())
                {
                    sb.Append(DumpTree(node.Children, indentLevel + 1));
                }
            }
            return sb.ToString();
        }
    }
} 