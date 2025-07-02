using Microsoft.AspNetCore.Components;
using BusinessEntity.Models;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;

namespace BusinessEntity.Components
{
    public partial class TreeComponent : ComponentBase
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public ILogger<TreeComponent> Logger { get; set; } = default!;

        [Parameter] public EventCallback<TreeNodeItem> OnNodeSelected { get; set; }
        
        private IEnumerable<TreeNodeItem> TreeData { get; set; } = new List<TreeNodeItem>();
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

        private async Task<IEnumerable<TreeNodeItem>> BuildTreeAsync()
        {
            try
            {
                var rootEntities = await BusinessEntityHelper.GetRootEntitiesAsync();
                var treeNodes = new List<TreeNodeItem>();

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
                return new List<TreeNodeItem>();
            }
        }

        private async Task<TreeNodeItem> BuildTreeNodeAsync(BusinessEntity.Core.Classes.BusinessEntity entity)
        {
            var icon = GetEntityIcon(entity.EntityType);
            var treeNode = new TreeNodeItem
            {
                Title = entity.Name,
                Icon = icon,
                Entity = entity,
                EntityType = entity.EntityType.ToString(),
                Expanded = true
            };

            // Получаем дочерние сущности
            var children = await BusinessEntityHelper.GetChildEntitiesAsync(entity.Id);
            var childNodes = new List<TreeNodeItem>();

            foreach (var child in children)
            {
                var childNode = await BuildTreeNodeAsync(child);
                childNodes.Add(childNode);
            }

            treeNode.Children = childNodes;
            return treeNode;
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
            if (data is TreeNodeItem node)
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
    }
} 