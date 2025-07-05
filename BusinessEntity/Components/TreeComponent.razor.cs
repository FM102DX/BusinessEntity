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
    public partial class TreeComponent : ComponentBase, IDisposable
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public ILogger<TreeComponent> Logger { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }
        [Inject] public IUserContextService UserContextService { get; set; } = default!;

        [Parameter] public EventCallback<TreeNodeItemViewModelBase> OnNodeSelected { get; set; }
        
        private IEnumerable<TreeNodeItemViewModelBase> TreeData { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsLoading { get; set; } = true;
        private bool Visible { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                IsLoading = true;
                
                // Инициализируем демо-данные если их нет
                await SampleDataService.InitializeSampleDataAsync();
                
                // Подписываемся на изменения выбранного пространства
                UserContextService.SelectedSpaceChanged += OnSelectedSpaceChanged;
                
                // Проверяем текущее состояние пространства
                await UpdateTreeForCurrentSpace();
                
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

        private async void OnSelectedSpaceChanged(Guid? spaceId)
        {
            try
            {
                await UpdateTreeForCurrentSpace();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling space change in TreeComponent");
            }
        }

        private async Task UpdateTreeForCurrentSpace()
        {
            if (!UserContextService.HasSelectedSpace)
            {
                // Если пространство не выбрано - скрываем дерево
                TreeData = new List<TreeNodeItemViewModelBase>();
                Visible = false;
                Logger.LogInformation("No space selected - hiding tree");
                return;
            }

            // Если пространство выбрано - строим дерево для этого пространства
            IsLoading = true;
            TreeData = await BuildTreeForSpaceAsync(UserContextService.CurrentSpaceId!.Value);
            Visible = true;
            IsLoading = false;
            
            Logger.LogInformation($"Tree built for space: {UserContextService.CurrentSpaceName}");
        }

        private async Task<IEnumerable<TreeNodeItemViewModelBase>> BuildTreeAsync()
        {
            try
            {
                var allBusinessEntities = await BusinessEntityHelper.GetAllBusinessEntities();
                var treeNodes = new List<TreeNodeItemViewModelBase>();

                if (UserContextService.HasSelectedSpace)
                {
                    var currentSpace = allBusinessEntities.FirstOrDefault(e => e.Id == UserContextService.CurrentSpaceId);
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
                    var rootSpaces = allBusinessEntities.Where(x => x.EntityType == BusinessEntityTypeEnum.Space);
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
            if (!UserContextService.HasSelectedSpace)
            {
                Logger.LogWarning("Cannot refresh tree - no space selected");
                return;
            }
            
            IsLoading = true;
            StateHasChanged();
            
            try
            {
                await UpdateTreeForCurrentSpace();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }        }

        public async Task CreateAdditionalTestDataAndRefreshAsync()
        {
            if (!UserContextService.HasSelectedSpace)
            {
                Logger.LogWarning("Cannot create test data - no space selected");
                return;
            }

            IsLoading = true;
            StateHasChanged();
            
            try
            {
                // Создаем дополнительные тестовые данные в выбранном пространстве
                await SampleDataService.InitializeSampleDataAsync();
                await UpdateTreeForCurrentSpace();
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
        
        private async Task<IEnumerable<TreeNodeItemViewModelBase>> BuildTreeForSpaceAsync(Guid spaceId)
        {
            try
            {
                // Получаем дочерние сущности выбранного пространства (не включая само пространство)
                var childEntities = await BusinessEntityHelper.GetChildEntitiesAsync(spaceId);
                var treeNodes = new List<TreeNodeItemViewModelBase>();

                foreach (var entity in childEntities)
                {
                    var treeNode = await BuildTreeNodeAsync(entity);
                    treeNodes.Add(treeNode);
                }

                return treeNodes;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error building tree for space {SpaceId}", spaceId);
                return new List<TreeNodeItemViewModelBase>();
            }
        }

        public void Dispose()
        {
            // Отписываемся от события при уничтожении компонента
            if (UserContextService != null)
            {
                UserContextService.SelectedSpaceChanged -= OnSelectedSpaceChanged;
            }
        }
    }
}