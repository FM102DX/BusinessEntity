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
        [Inject] public BusinessEntity.Core.Services.BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
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
                await DrawTreeForCurrentSpace();
                
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
                await DrawTreeForCurrentSpace();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling space change in TreeComponent");
            }
        }

        private async Task DrawTreeForCurrentSpace()
        {
            var space = await SpaceHelper.GetSpaceByIdAsync(UserContextService.CurrentSpaceId!.Value);
            if (space == null)
            {
                // Если пространство не выбрано - скрываем дерево
                TreeData = new List<TreeNodeItemViewModelBase>();
                Visible = false;
                return;
            }
            IsLoading = true;
            var rootSpaceNode = await BuildSpaceRootAsync(space);
            TreeData = new[] { rootSpaceNode };
            Visible = true;
            IsLoading = false;
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
            var rootEntities = await BusinessEntityHelper.GetContainedEntitiesAsync(space.Id);
            var childNodes = new List<TreeNodeItemViewModelBase>();

            foreach (var entity in rootEntities)
            {
                var childNode = await BuildTreeNodeAsync(entity);
                childNodes.Add(childNode);
            }

            rootVm.Children = childNodes;
            return rootVm;
        }

        private async Task<TreeNodeItemViewModelBase> BuildTreeNodeAsync(BusinessEntity.Core.Classes.BusinessEntity entity)
        {
            var icon = GetEntityIcon(entity.EntityType);
              // Создаем соответствующий тип наследника в зависимости от типа сущности
            // Space не обрабатываем здесь, так как он создается через BuildSpaceRootAsync
            TreeNodeItemViewModelBase treeNodeVm = entity.EntityType.ToString() switch
            {
                "Folder" => new FolderTreeNodeItemViewModel(entity, WebLogger),
                "Document" => new DocumentTreeNodeItemViewModel(WebLogger),
                "Page" => new DocumentTreeNodeItemViewModel(WebLogger),
                _ => new FolderTreeNodeItemViewModel(entity, WebLogger) // По умолчанию используем Folder
            };

            // Заполняем общие свойства (некоторые уже заполнены в конструкторе для Folder)
            if (entity.EntityType.ToString() != "Folder")
            {
                treeNodeVm.Title = entity.Name;
                treeNodeVm.Entity = entity;
                treeNodeVm.EntityType = entity.EntityType.ToString();
            }
            treeNodeVm.Icon = icon;
            treeNodeVm.Expanded = true;

            // Устанавливаем обратный вызов для создания сущностей у папок
            if (entity.EntityType.ToString() == "Folder")
            {
                treeNodeVm.OnEntityCreateRequested = OnEntityCreateRequestedAsync;
            }

            // Получаем дочерние сущности через BusinessEntityHelper для получения детей
            var children = await BusinessEntityHelper.GetContainedEntitiesAsync(entity.Id);
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
        
       private void OnTreeContextMenu(TreeItemContextMenuEventArgs args)
        {
            // Получаем выбранный узел дерева
            var selectedNode = args.Value as TreeNodeItemViewModelBase;
            if (selectedNode == null)
            {
                return;
            }

            // Получаем пункты меню из модели
            var menuItems = selectedNode.CreateContextMenu();

            // TreeItemContextMenuEventArgs уже наследуется от MouseEventArgs, поэтому используем args напрямую
            ContextMenu.Open(args, menuItems, async (item) =>
            {
                try
                {
                    // Напрямую вызываем обработчик в ViewModel
                    var actionValue = item?.Value?.ToString();
                    if (!string.IsNullOrEmpty(actionValue))
                    {
                        await selectedNode.HandleMenuActionAsync(actionValue);
                    }
                }
                finally
                {
                    // Закрываем контекстное меню после выполнения действия
                    ContextMenu.Close();
                }
            });
        }        
       public void Dispose()
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

                switch (entityType)
                {
                    case "Folder":
                        // Создаем новую папку через BusinessEntityHelper
                        var newEntity = await BusinessEntityHelper.CreateSubFolderAsync(parentNode.Entity);
                        
                        // Создаем view model для новой папки используя новый конструктор
                        var childNode = new FolderTreeNodeItemViewModel(newEntity, WebLogger)
                        {
                            Icon = GetEntityIcon(newEntity.EntityType),
                            Expanded = false,
                            Parent = parentNode,
                            OnEntityCreateRequested = OnEntityCreateRequestedAsync // Устанавливаем колбэк для новой папки
                        };
                        
                        // Добавляем новую ноду в дерево
                        parentNode.Children.Add(childNode);
                        parentNode.Expanded = true; // Разворачиваем родительскую ноду
                        
                        Logger.LogInformation($"Successfully created folder '{newEntity.Name}' under '{parentNode.Entity.Name}'");
                        break;
                        
                    default:
                        Logger.LogInformation($"TODO: Create {entityType} entity under {parentNode.Entity.Name}");
                        break;
                }

                // Обновляем UI
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