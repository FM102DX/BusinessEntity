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
using Radzen.Blazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BusinessEntity.Components
{
    public partial class TreeComponent : ComponentBase, IDisposable
    {
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntity.Core.Services.BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public SpaceHelper SpaceHelper { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }
        [Inject] public IUserContextService UserContextService { get; set; } = default!;
        [Inject] public ContextMenuService ContextMenu { get; set; } = default!;
        [Inject] public ITreeSelectionService TreeSelectionService { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

        [Parameter] public EventCallback<TreeNodeItemViewModelBase> OnNodeSelected { get; set; }
        [Parameter] public EventCallback<List<TreeNodeItemViewModelBase>> OnMultipleNodesSelected { get; set; }
        
        private IEnumerable<TreeNodeItemViewModelBase> TreeData { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsLoading { get; set; } = true;
        private bool Visible { get; set; } = false;
        
        // Состояние мульти-селекта
        private List<TreeNodeItemViewModelBase> SelectedNodes { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsMultiSelectMode { get; set; } = false;
        
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
                
                WebLogger?.Information("TreeComponent initialized successfully");
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
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
                WebLogger?.Error(ex);
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
            
            // Очищаем выбранные узлы при смене пространства
            await ClearAllSelections();


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
                var icon = GetEntityIcon(entityType);
                // Убираем галочку, оставляем только иконку и название
                return $"{icon} {node.Title}";
            }
            return "Unknown";
        }

        // Обработчик изменений в дереве (пустой, так как используем прямые клики)
        private async Task OnTreeNodeClick(TreeEventArgs args)
        {
            // Логику выбора обрабатываем в OnNodeClicked в razor-файле
            await Task.CompletedTask;
        }

        // Публичный метод для обработки кликов по узлам (вызывается из razor)
        public async Task OnNodeClicked(TreeNodeItemViewModelBase? node)
        {
            if (node == null) return;

            // Получаем состояние клавиш из JavaScript
            var keyState = await JSRuntime.InvokeAsync<System.Text.Json.JsonElement>("TreeMultiSelect.getKeyState");

            bool ctrlPressed = false;
            bool shiftPressed = false;

            // Правильно парсим JsonElement
            if (keyState.TryGetProperty("ctrl", out var ctrlProperty))
            {
                ctrlPressed = ctrlProperty.GetBoolean();
            }

            if (keyState.TryGetProperty("shift", out var shiftProperty))
            {
                shiftPressed = shiftProperty.GetBoolean();
            }

            await HandleNodeSelection(node, ctrlPressed, shiftPressed);
        }

        // Обработка выбора узла с учетом модификаторов клавиш
        private async Task HandleNodeSelection(TreeNodeItemViewModelBase clickedNode, bool isCtrlPressed, bool isShiftPressed)
        {
            if (clickedNode == null) return;

            WebLogger?.Information($"HandleNodeSelection started for node: {clickedNode.Title}, Ctrl: {isCtrlPressed}, Shift: {isShiftPressed}");

            // Игнорируем Shift+click (запрещаем выбор диапазона)
            if (isShiftPressed) 
            {
                WebLogger?.Information("Shift+click ignored");
                return;
            }

            if (isCtrlPressed)
            {
                // Ctrl+click: добавить/убрать из выделения
                if (clickedNode.IsSelected)
                {
                    WebLogger?.Information($"Ctrl+click: deselecting node {clickedNode.Title}");
                    clickedNode.SetSelected(false);
                    SelectedNodes.Remove(clickedNode);
                }
                else
                {
                    WebLogger?.Information($"Ctrl+click: selecting node {clickedNode.Title}");
                    clickedNode.SetSelected(true);
                    SelectedNodes.Add(clickedNode);
                }
            }
            else
            {
                // Обычный click: полностью очистить все выделения и выделить только текущий узел
                WebLogger?.Information($"Normal click: clearing all selections and selecting {clickedNode.Title}");
                await ClearAllSelections(); // ← ИСПРАВЛЕНО: добавлен await
                WebLogger?.Information($"After ClearAllSelections, now selecting {clickedNode.Title}");
                clickedNode.SetSelected(true);
                SelectedNodes.Add(clickedNode);
                WebLogger?.Information($"Node {clickedNode.Title} selected, SelectedNodes count: {SelectedNodes.Count}");
            }

            // Обновляем состояние мульти-селекта
            IsMultiSelectMode = SelectedNodes.Count > 1;
            WebLogger?.Information($"IsMultiSelectMode set to: {IsMultiSelectMode}");
            
            // Обновляем сервис выделения
            TreeSelectionService.SetSelectedNodes(SelectedNodes.ToList());
            WebLogger?.Information($"TreeSelectionService updated with {SelectedNodes.Count} nodes");
            
            // Принудительно обновляем UI
            await InvokeAsync(StateHasChanged);
            WebLogger?.Information("HandleNodeSelection completed");
        }

        // Новый метод для полной очистки всех выделений
        public async Task ClearAllSelections()
        {
            WebLogger?.Information($"ClearAllSelections started. Current SelectedNodes count: {SelectedNodes.Count} They are: {String.Join(", ",SelectedNodes.Select(x=>x.Title).ToList())}");
            
            SelectedNodes.Clear();

            WebLogger?.Information("SelectedNodes collection cleared");
            
            // Затем рекурсивно проходим по всему дереву и принудительно снимаем выделение
            if (TreeData != null)
            {
                WebLogger?.Information($"Processing {TreeData.Count()} root nodes");
                foreach (var rootNode in TreeData)
                {
                    ForceCleanSelectionRecursive(rootNode);
                }
            }
            IsMultiSelectMode = false;
            TreeSelectionService.ClearSelection();
            await InvokeAsync(StateHasChanged);
            await Task.Delay(1); // Небольшая задержка для обновления DOM
            await InvokeAsync(StateHasChanged);
            // Принудительно обновляем CSS-классы через JavaScript
            await JSRuntime.InvokeAsync<int>("TreeMultiSelect.forceRefreshTreeSelection");
        }

        // Принудительная очистка выделения для всех узлов
        private void ForceCleanSelectionRecursive(TreeNodeItemViewModelBase node)
        {
            if (node == null) return;
            bool wasSelected = node.IsSelected;
            node.SetSelected(false);
            node.IsDragging = false;
            // Рекурсивно обрабатываем дочерние узлы
            if (node.Children != null)
            {
                foreach (var child in node.Children.Cast<TreeNodeItemViewModelBase>())
                {
                    ForceCleanSelectionRecursive(child);
                }
            }
        }

        // Рекурсивный поиск выделенных узлов
        private void FindSelectedNodesRecursive(TreeNodeItemViewModelBase node, List<TreeNodeItemViewModelBase> selectedNodes)
        {
            if (node == null) return;
            
            if (node.IsSelected)
            {
                selectedNodes.Add(node);
            }
            
            if (node.Children != null)
            {
                foreach (var child in node.Children.Cast<TreeNodeItemViewModelBase>())
                {
                    FindSelectedNodesRecursive(child, selectedNodes);
                }
            }
        }

        private void SelectAllNodesRecursive(IEnumerable<TreeNodeItemViewModelBase> nodes)
        {
            foreach (var node in nodes)
            {
                node.SetSelected(true);
                SelectedNodes.Add(node);
                
                if (node.Children?.Any() == true)
                {
                    SelectAllNodesRecursive(node.Children);
                }
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
                    WebLogger?.Warning("Cannot create entity - parent node or entity is null");
                    return;
                }

                WebLogger?.Information($"Creating new {entityType} in parent {parentNode.Entity.Name}");

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
                        
                        WebLogger?.Information($"Successfully created folder '{newEntity.Name}' under '{parentNode.Entity.Name}'");
                        break;
                        
                    default:
                        WebLogger?.Information($"TODO: Create {entityType} entity under {parentNode.Entity.Name}");
                        break;
                }

                // Обновляем UI
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
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

        #region Drag & Drop Handlers

        /// <summary>
        /// Обработчик начала перетаскивания узла
        /// </summary>
        private async Task OnDragStart(DragEventArgs e, TreeNodeItemViewModelBase? draggedNode)
        {
            try
            {
                if (draggedNode == null)
                {
                    WebLogger?.Warning("OnDragStart: dragged node is null");
                    return;
                }

                WebLogger?.Information($"Drag started for node: {draggedNode.Title}");

                // Проверяем, входит ли перетаскиваемый узел в выбранные
                if (!SelectedNodes.Contains(draggedNode))
                {
                    // Если узел не выбран, очищаем выбор и выбираем только его
                    await ClearAllSelections();
                    draggedNode.SetSelected(true);
                    SelectedNodes.Add(draggedNode);
                    TreeSelectionService.SetSelectedNodes(SelectedNodes);
                }

                // Помечаем все выбранные узлы как перетаскиваемые
                foreach (var node in SelectedNodes)
                {
                    node.SetDragging(true);
                }

                // Информация о перетаскиваемых узлах сохраняется в поле SelectedNodes
                var draggedTitles = SelectedNodes.Select(n => n.Title).ToList();

                WebLogger?.Information($"Dragging {SelectedNodes.Count} selected nodes");
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
        }

        /// <summary>
        /// Обработчик перетаскивания над целью
        /// </summary>
        private void OnDragOver(DragEventArgs e)
        {
            // Разрешаем drop операцию
            // В Blazor preventDefault() выполняется через атрибут @ondragover:preventDefault="true"
        }

        /// <summary>
        /// Обработчик входа в зону drop
        /// </summary>
        private void OnDragEnter(DragEventArgs e, TreeNodeItemViewModelBase? targetNode)
        {
            if (targetNode != null && CanDropToTarget(targetNode))
            {
                // Добавляем CSS класс для визуального индикатора
                // Это потребует дополнительной логики состояния
            }
        }

        /// <summary>
        /// Обработчик выхода из зоны drop
        /// </summary>
        private void OnDragLeave(DragEventArgs e, TreeNodeItemViewModelBase? targetNode)
        {
            // Убираем визуальный индикатор
        }

        /// <summary>
        /// Обработчик завершения перетаскивания (drop)
        /// </summary>
        private async Task OnDrop(DragEventArgs e, TreeNodeItemViewModelBase? targetNode)
        {
            try
            {
                if (targetNode == null)
                {
                    WebLogger?.Warning("OnDrop: target node is null");
                    return;
                }

                // Проверяем, можно ли дропнуть в эту цель
                if (!CanDropToTarget(targetNode))
                {
                    WebLogger?.Information($"Drop cancelled: cannot drop to {targetNode.EntityType} '{targetNode.Title}'");
                    return;
                }

                // Получаем список перетаскиваемых узлов
                var draggedNodes = SelectedNodes.Where(n => n.IsDragging).ToList();
                if (!draggedNodes.Any())
                {
                    WebLogger?.Warning("OnDrop: no dragged nodes found");
                    return;
                }

                // Проверяем, не пытаемся ли дропнуть узел в самого себя или своих потомков
                if (IsDropToSelfOrDescendant(draggedNodes, targetNode))
                {
                    WebLogger?.Information("Drop cancelled: cannot drop node to itself or its descendant");
                    return;
                }

                // Логируем операцию
                var draggedTitles = draggedNodes.Select(n => n.Title).ToList();
                WebLogger?.Information($"Dropped: [{string.Join(", ", draggedTitles)}] -> {targetNode.Title} (ID: {targetNode.Entity?.Id})");

                // TODO: Здесь будет реальная бизнес-логика перемещения
                // Пока что только логируем операцию

                // Убираем флаги перетаскивания
                ClearDraggingFlags();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
        }

        /// <summary>
        /// Обработчик окончания перетаскивания (независимо от результата)
        /// </summary>
        private async Task OnDragEnd(DragEventArgs e, TreeNodeItemViewModelBase? draggedNode)
        {
            try
            {
                WebLogger?.Information("Drag operation ended");
                
                // Убираем флаги перетаскивания у всех узлов
                ClearDraggingFlags();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
        }

        /// <summary>
        /// Проверяет, можно ли дропнуть в указанную цель
        /// </summary>
        private bool CanDropToTarget(TreeNodeItemViewModelBase targetNode)
        {
            // Можно дропать только в папки и пространства
            return targetNode.EntityType == "Folder" || targetNode.EntityType == "Space";
        }

        /// <summary>
        /// Проверяет, не пытаемся ли дропнуть узел в самого себя или своих потомков
        /// </summary>
        private bool IsDropToSelfOrDescendant(List<TreeNodeItemViewModelBase> draggedNodes, TreeNodeItemViewModelBase targetNode)
        {
            foreach (var draggedNode in draggedNodes)
            {
                // Проверяем, не является ли цель самим перетаскиваемым узлом
                if (draggedNode == targetNode)
                    return true;

                // Проверяем, не является ли цель потомком перетаскиваемого узла
                if (IsDescendantOf(targetNode, draggedNode))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Проверяет, является ли node потомком ancestor
        /// </summary>
        private bool IsDescendantOf(TreeNodeItemViewModelBase node, TreeNodeItemViewModelBase ancestor)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Убирает флаги перетаскивания у всех узлов
        /// </summary>
        private void ClearDraggingFlags()
        {
            ClearDraggingFlagsRecursive(TreeData);
        }

        /// <summary>
        /// Рекурсивно убирает флаги перетаскивания
        /// </summary>
        private void ClearDraggingFlagsRecursive(IEnumerable<TreeNodeItemViewModelBase> nodes)
        {
            foreach (var node in nodes)
            {
                node.SetDragging(false);
                
                if (node.Children?.Any() == true)
                {
                    ClearDraggingFlagsRecursive(node.Children);
                }
            }
        }

        #endregion


    }
}