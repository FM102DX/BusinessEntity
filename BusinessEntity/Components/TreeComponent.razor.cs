using Microsoft.AspNetCore.Components;
using BusinessEntity.Models;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;
using SampleOnlineMall.WebLogger.Services;
using System.Linq;
using BusinessEntity.Contracts;
using BusinessEntity.Services;
using Radzen;

namespace BusinessEntity.Components
{
    public partial class TreeComponent : ComponentBase, IDisposable
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public SpaceHelper SpaceHelper { get; set; } = default!;
        [Inject] public ILogger<TreeComponent> Logger { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }
        [Inject] public IUserContextService UserContextService { get; set; } = default!;
        [Inject] public ContextMenuService ContextMenu { get; set; } = default!;

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

            // Если пространство выбрано - строим дерево с корневым пространством
            IsLoading = true;
            
            var space = await SpaceHelper.GetAsync(UserContextService.CurrentSpaceId!.Value);
            if (space == null)
            {
                Logger.LogWarning("Space with ID {SpaceId} not found", UserContextService.CurrentSpaceId);
                TreeData = new List<TreeNodeItemViewModelBase>();
                Visible = false;
                IsLoading = false;
                return;
            }

            var rootSpaceNode = await BuildSpaceRootAsync(space);
            TreeData = new[] { rootSpaceNode };
            Visible = true;
            IsLoading = false;
            
            Logger.LogInformation($"Tree built for space: {UserContextService.CurrentSpaceName}");
        }

        private async Task<SpaceTreeNodeItemViewModel> BuildSpaceRootAsync(BusinessEntity.Core.Classes.BusinessEntity space)
        {
            var rootVm = new SpaceTreeNodeItemViewModel(WebLogger)
            {
                Title = space.Name,
                Icon = GetEntityIcon(space.EntityType),
                Entity = space,
                EntityType = space.EntityType.ToString(),
                Expanded = true,
                // Устанавливаем обратный вызов для создания сущностей
                OnEntityCreateRequested = OnEntityCreateRequestedAsync
            };

            // Получаем все элементы верхнего уровня в пространстве через BusinessEntityHelper
            var rootEntities = await BusinessEntityHelper.GetChildEntitiesAsync(space.Id);
            var childNodes = new List<TreeNodeItemViewModelBase>();

            foreach (var entity in rootEntities)
            {
                var childNode = await BuildTreeNodeAsync(entity);
                childNodes.Add(childNode);
            }

            rootVm.Children = childNodes;
            return rootVm;
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
        }        private async Task<TreeNodeItemViewModelBase> BuildTreeNodeAsync(BusinessEntity.Core.Classes.BusinessEntity entity)
        {
            var icon = GetEntityIcon(entity.EntityType);
              // Создаем соответствующий тип наследника в зависимости от типа сущности
            // Space не обрабатываем здесь, так как он создается через BuildSpaceRootAsync
            TreeNodeItemViewModelBase treeNodeVm = entity.EntityType.ToString() switch
            {
                "Folder" => new FolderTreeNodeItemViewModel(WebLogger),
                "Document" => new DocumentTreeNodeItemViewModel(WebLogger),
                "Page" => new DocumentTreeNodeItemViewModel(WebLogger),
                _ => new FolderTreeNodeItemViewModel(WebLogger) // По умолчанию используем Folder
            };

            // Заполняем общие свойства
            treeNodeVm.Title = entity.Name;
            treeNodeVm.Icon = icon;
            treeNodeVm.Entity = entity;
            treeNodeVm.EntityType = entity.EntityType.ToString();
            treeNodeVm.Expanded = true;

            // Получаем дочерние сущности через BusinessEntityHelper для получения детей
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

        private string GetEntityIcon(object entityType)
        {
            return entityType?.ToString() switch
            {
                "Space" => "📁",
                "Folder" => "📂",
                "Document" => "📄",
                "Page" => "📄",
                _ => "❓"
            };
        }

        private string GetNodeText(object data)
        {
            if (data is TreeNodeItemViewModelBase node)
            {
                var entityType = node.Entity?.EntityType.ToString() ?? "Space";
                return $"{GetEntityIcon(entityType)} {node.Title}";
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
        }        private void OnTreeContextMenu(TreeItemContextMenuEventArgs args)
        {
            // Получаем выбранный узел дерева
            var selectedNode = args.Value as TreeNodeItemViewModelBase;
            if (selectedNode == null)
            {
                Logger.LogWarning("Unable to get selected tree node from context menu args");
                return;
            }

            // Получаем пункты меню из модели
            var menuItems = selectedNode.CreateContextMenu();

            // TreeItemContextMenuEventArgs уже наследуется от MouseEventArgs, поэтому используем args напрямую
            ContextMenu.Open(args, menuItems, async (item) =>
            {
                if (WebLogger != null)
                {
                    var selectedText = item?.Text ?? "Неизвестный пункт";
                    var nodeType = selectedNode.MenuText;
                    await WebLogger.Information($"Выбран пункт '{selectedText}' для {nodeType} '{selectedNode.Title}'");
                }
                  // Напрямую вызываем обработчик в ViewModel
                var actionValue = item?.Value?.ToString();
                if (!string.IsNullOrEmpty(actionValue))
                {
                    await selectedNode.HandleMenuActionAsync(actionValue);
                }
            });
        }        public void Dispose()
        {
            // Отписываемся от события при уничтожении компонента
            if (UserContextService != null)
            {
                UserContextService.SelectedSpaceChanged -= OnSelectedSpaceChanged;
            }
        }

        /// <summary>
        /// Обработчик запроса на создание новой сущности
        /// </summary>
        private async Task OnEntityCreateRequestedAsync(TreeNodeItemViewModelBase parentNode, string entityType)
        {
            try
            {
                if (parentNode?.Entity == null)
                {
                    Logger.LogWarning("Cannot create entity - parent node or entity is null");
                    return;
                }

                Logger.LogInformation($"Creating new {entityType} in parent {parentNode.Entity.Name}");

                // Пока что просто логируем. TODO: Реализовать создание сущности когда найдем правильный метод
                Logger.LogInformation($"TODO: Create {entityType} entity under {parentNode.Entity.Name}");
                
                // Имитируем успешное создание
                await Task.Delay(100);

                // Обновляем дерево
                await UpdateTreeForCurrentSpace();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error creating {entityType} entity");
            }
        }

        private bool IsNodeExpanded(object data)
        {
            if (data is TreeNodeItemViewModelBase node)
            {
                // Узлы пространства всегда развернуты
                if (node.Entity?.EntityType.ToString() == "Space")
                {
                    return true;
                }
                return node.Expanded;
            }
            return false;
        }
    }
}