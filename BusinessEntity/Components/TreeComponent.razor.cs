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
        [Inject] public BusinessEntity.Core.Services.BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public SpaceHelper SpaceHelper { get; set; } = default!;
        [Inject] IWebLoggerService? WebLogger { get; set; }
        [Inject] public IUserContextService UserContextService { get; set; } = default!;
        [Inject] public ContextMenuService ContextMenu { get; set; } = default!;
        [Inject] public ITreeSelectionService TreeSelectionService { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        [Parameter] public EventCallback<TreeNodeItemViewModelBase> OnNodeSelected { get; set; }
        [Parameter] public EventCallback<List<TreeNodeItemViewModelBase>> OnMultipleNodesSelected { get; set; }
        
        private IEnumerable<TreeNodeItemViewModelBase> TreeData { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsLoading { get; set; } = true;
        private bool Visible { get; set; } = false;
        
        // Состояние мульти-селекта
        private List<TreeNodeItemViewModelBase> SelectedNodes { get; set; } = new List<TreeNodeItemViewModelBase>();
        private bool IsMultiSelectMode { get; set; } = false;
        
        // Состояние inline-редактирования
        private TreeNodeItemViewModelBase? EditingNode { get; set; } = null;
        private string EditingText { get; set; } = string.Empty;
        private ElementReference editingInput;
        
        // Состояние валидации при редактировании
        private bool HasValidationError { get; set; } = false;
        private string ValidationErrorMessage { get; set; } = string.Empty;
        
        protected override async Task OnInitializedAsync()
        {
            try
            {
                IsLoading = true;
                
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
                OnEntityCreateRequested = OnEntityCreateRequestedAsync,
                // Устанавливаем обратный вызов для удаления сущностей
                OnEntityDeleteRequested = OnEntityDeleteRequestedAsync,
                // Устанавливаем обратный вызов для переименования сущностей
                OnEntityRenameRequested = OnEntityRenameRequestedAsync
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
            
            // Устанавливаем обратный вызов для удаления сущностей для всех типов узлов
            treeNodeVm.OnEntityDeleteRequested = OnEntityDeleteRequestedAsync;
            
            // Устанавливаем обратный вызов для переименования сущностей для всех типов узлов
            treeNodeVm.OnEntityRenameRequested = OnEntityRenameRequestedAsync;

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
            // Map to Material icon names used by RadzenIcon
            return entityType?.ToString() switch
            {
                "Space" => "dashboard",            // or "account_tree" / "layers"
                "Folder" => "folder",
                "Document" => "description",       // alternatively: "insert_drive_file" / "article"
                "Page" => "insert_drive_file",
                _ => "insert_drive_file"
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

        private bool NodeHasChildren(object data)
        {
            if (data is TreeNodeItemViewModelBase node)
            {
                return node.Children?.Any() == true;
            }
            return false;
        }

        // Публичный метод для обработки кликов по узлам (вызывается из razor)
        public async Task OnNodeClicked(TreeNodeItemViewModelBase? node)
        {
            //WebLogger?.Information($"[OnNodeClicked]--Enter");
            Console.WriteLine($"[OnNodeClicked]--Enter");
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

        // Централизованный обработчик двойного клика по узлу
        public async Task OnNodeDoubleClicked(TreeNodeItemViewModelBase? node)
        {
            try
            {
                if (node?.Entity == null)
                {
                    return;
                }

                var entity = node.Entity;
                WebLogger?.Information($"DoubleClick on '{entity.Name}' ({entity.EntityType}), ID={entity.Id}");

                switch (entity.EntityType)
                {
                    case BusinessEntityTypeEnum.Document:
                        NavigationManager.NavigateTo($"/document/{entity.Id}");
                        break;
                    // В будущем можно добавить другие типы
                    // case BusinessEntityTypeEnum.Page:
                    //     NavigationManager.NavigateTo($"/page/{entity.Id}");
                    //     break;
                    default:
                        // Для папок/пространств пока ничего не делаем
                        break;
                }
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
            await Task.CompletedTask;
        }

        // Обработка выбора узла с учетом модификаторов клавиш
        private async Task HandleNodeSelection(TreeNodeItemViewModelBase clickedNode, bool isCtrlPressed, bool isShiftPressed)
        {

            //WebLogger?.Information($"[HandleNodeSelection]--Enter; started for node: {clickedNode.Title}, Ctrl: {isCtrlPressed}, Shift: {isShiftPressed}");
            Console.WriteLine($"[HandleNodeSelection]--Enter; started for node: {clickedNode.Title}, Ctrl: {isCtrlPressed}, Shift: {isShiftPressed}");
            if (clickedNode == null) return;
            
            // Игнорируем Shift+click (запрещаем выбор диапазона)
            if (isShiftPressed) 
            {
                return;
            }

            if (isCtrlPressed)
            {
                // Ctrl+click: добавить/убрать из выделения
                if (clickedNode.IsSelected)
                {
                    clickedNode.SetSelected(false);
                    SelectedNodes.Remove(clickedNode);
                }
                else
                {
                    clickedNode.SetSelected(true);
                    SelectedNodes.Add(clickedNode);
                }
            }
            else
            {
                // Обычный click: полностью очистить все выделения и выделить только текущий узел
                await ClearAllSelections();
                clickedNode.SetSelected(true);
                SelectedNodes.Add(clickedNode);
            }

            // Обновляем состояние мульти-селекта
            IsMultiSelectMode = SelectedNodes.Count > 1;
            
            // Обновляем сервис выделения
            TreeSelectionService.SetSelectedNodes(SelectedNodes.ToList());
            
            // Принудительно обновляем UI
            await InvokeAsync(StateHasChanged);
        }

        // Новый метод для полной очистки всех выделений
        public async Task ClearAllSelections()
        {
            //WebLogger?.Information($"[ClearAllSelections]--Enter. Current SelectedNodes count: {SelectedNodes.Count} They are: {String.Join(", ",SelectedNodes.Select(x=>x.Title).ToList())}");
            Console.WriteLine($"[ClearAllSelections]--Enter. Current SelectedNodes count: {SelectedNodes.Count} They are: {String.Join(", ", SelectedNodes.Select(x => x.Title).ToList())}");
            SelectedNodes.Clear();

            // Затем рекурсивно проходим по всему дереву и принудительно снимаем выделение
            if (TreeData != null)
            {
                foreach (var rootNode in TreeData)
                {
                    ForceCleanSelectionRecursive(rootNode);
                }
            }
            IsMultiSelectMode = false;
            TreeSelectionService.ClearSelection();
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
                            OnEntityCreateRequested = OnEntityCreateRequestedAsync, // Устанавливаем колбэк для новой папки
                            OnEntityDeleteRequested = OnEntityDeleteRequestedAsync, // Устанавливаем колбэк для удаления новой папки
                            OnEntityRenameRequested = OnEntityRenameRequestedAsync // Устанавливаем колбэк для переименования новой папки
                        };
                        
                        // Добавляем новую ноду в дерево
                        parentNode.Children.Add(childNode);
                        parentNode.Expanded = true; // Разворачиваем родительскую ноду
                        
                        WebLogger?.Information($"Successfully created folder '{newEntity.Name}' under '{parentNode.Entity.Name}'");
                        break;
                        
                    case "Document":
                        {
                            // Создаем документ через BusinessEntityHelper
                            var newDoc = await BusinessEntityHelper.CreateDocumentAsync(parentNode.Entity);

                            // Создаем view model для документа
                            var docNode = new DocumentTreeNodeItemViewModel(WebLogger)
                            {
                                Title = newDoc.Name,
                                Icon = GetEntityIcon(newDoc.EntityType),
                                Entity = newDoc,
                                EntityType = newDoc.EntityType.ToString(),
                                Parent = parentNode,
                                OnEntityDeleteRequested = OnEntityDeleteRequestedAsync
                            };

                            // Добавляем в дерево и разворачиваем родителя
                            parentNode.Children.Add(docNode);
                            parentNode.Expanded = true;

                            WebLogger?.Information($"Successfully created document '{newDoc.Name}' under '{parentNode.Entity.Name}'");
                        }
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

        /// <summary>
        /// Обработчик запроса на удаление сущности или нескольких сущностей
        /// </summary>
        private async Task OnEntityDeleteRequestedAsync(TreeNodeItemViewModelBase nodeToDelete)
        {
            try
            {
                // Определяем, какие элементы будут удалены
                var nodesToDelete = new List<TreeNodeItemViewModelBase>();
                
                // Если есть мультиселект и удаляемый узел входит в выделенные, удаляем все выделенные
                if (SelectedNodes.Count > 1 && SelectedNodes.Contains(nodeToDelete))
                {
                    nodesToDelete.AddRange(SelectedNodes);
                }
                else
                {
                    // Иначе удаляем только тот узел, на котором была вызвана команда
                    nodesToDelete.Add(nodeToDelete);
                }

                // Подсчитываем количество элементов для удаления
                var count = nodesToDelete.Count;
                var message = count == 1 ? $"Удалить 1 элемент?" : $"Удалить {count} элементов?";

                // Показываем подтверждающий диалог
                var confirmed = await ShowConfirmationDialog(message);
                if (!confirmed)
                {
                    WebLogger?.Information("Пользователь отменил удаление");
                    return;
                }

                // Удаляем каждый элемент
                var deletedNodes = new List<TreeNodeItemViewModelBase>();
                foreach (var node in nodesToDelete)
                {
                    if (node?.Entity == null)
                    {
                        WebLogger?.Warning($"Cannot delete node - Entity is null");
                        continue;
                    }

                    WebLogger?.Information($"Deleting entity '{node.Title}' (ID: {node.Entity.Id})");
                    
                    try
                    {
                        // Удаляем через BusinessEntityHelper
                        await BusinessEntityHelper.RemoveBusinessEntity(node.Entity.Id);
                        WebLogger?.Information($"Successfully deleted entity '{node.Title}'");
                        deletedNodes.Add(node);
                    }
                    catch (Exception ex)
                    {
                        WebLogger?.Warning($"Failed to delete entity '{node.Title}': {ex.Message}");
                    }
                }

                // Удаляем успешно удаленные узлы из дерева
                foreach (var deletedNode in deletedNodes)
                {
                    RemoveNodeFromTree(deletedNode);
                }

                // Очищаем выделение после удаления
                await ClearAllSelections();

                // Принудительно обновляем дерево через JavaScript
                await JSRuntime.InvokeAsync<int>("TreeMultiSelect.forceRefreshTreeSelection");

                // Обновляем UI
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error(ex);
            }
        }

        /// <summary>
        /// Показывает диалог подтверждения удаления
        /// </summary>
        private async Task<bool> ShowConfirmationDialog(string message)
        {
            // Простая реализация через JavaScript confirm
            // В реальном проекте можно использовать более красивый диалог
            return await JSRuntime.InvokeAsync<bool>("confirm", message);
        }

        /// <summary>
        /// Удаляет узел из дерева
        /// </summary>
        private void RemoveNodeFromTree(TreeNodeItemViewModelBase nodeToRemove)
        {
            if (nodeToRemove == null) return;
            
            WebLogger?.Information($"Removing node '{nodeToRemove.Title}' from tree");
            
            // Рекурсивно удаляем узел из всего дерева
            RemoveNodeRecursively(TreeData, nodeToRemove);
            
            // Удаляем из списка выделенных узлов, если он там есть
            SelectedNodes.Remove(nodeToRemove);
            
            // Очищаем ссылки узла для предотвращения утечек памяти
            nodeToRemove.Parent = null;
            nodeToRemove.Children.Clear();
        }

        /// <summary>
        /// Рекурсивно ищет и удаляет узел из коллекций дерева
        /// </summary>
        private bool RemoveNodeRecursively(IEnumerable<TreeNodeItemViewModelBase> nodes, TreeNodeItemViewModelBase nodeToRemove)
        {
            if (nodes == null) return false;
            
            foreach (var node in nodes.ToList()) // ToList() для избежания изменения коллекции во время итерации
            {
                // Проверяем дочерние элементы текущего узла
                if (node.Children.Contains(nodeToRemove))
                {
                    node.Children.Remove(nodeToRemove);
                    return true;
                }
                
                // Рекурсивно проверяем дочерние элементы
                if (RemoveNodeRecursively(node.Children, nodeToRemove))
                {
                    return true;
                }
            }
            
            // Если это корневой элемент, удаляем из TreeData
            if (TreeData.Contains(nodeToRemove))
            {
                var treeDataList = TreeData.ToList();
                var removed = treeDataList.Remove(nodeToRemove);
                if (removed)
                {
                    TreeData = treeDataList;
                    WebLogger?.Information($"Removed root element '{nodeToRemove.Title}' from TreeData");
                    return true;
                }
            }
            
            return false;
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

                // Создаем всплывающий элемент с именами перетаскиваемых элементов
                await CreateDragTooltip();

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
        private async Task OnDragOver(DragEventArgs e)
        {
            // Разрешаем drop операцию
            // В Blazor preventDefault() выполняется через атрибут @ondragover:preventDefault="true"
            
            // Обновляем позицию всплывающего элемента
            try
            {
                await JSRuntime.InvokeVoidAsync("updateDragTooltipPosition", e.ClientX, e.ClientY);
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки обновления позиции, так как это не критично
                WebLogger?.Warning($"Failed to update tooltip position: {ex.Message}");
            }
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

                // Логируем операцию одной строкой в веб-логгер
                var draggedTitles = draggedNodes.Select(n => n.Title).ToList();
                var logMessage = $"DRAG_DROP_COMPLETED: [{string.Join(", ", draggedTitles)}] dropped to '{targetNode.Title}' (ID: {targetNode.Entity?.Id})";
                WebLogger?.Information(logMessage);

                // Удаляем всплывающий элемент
                await RemoveDragTooltip();

                // Реальная бизнес-логика перемещения
                foreach (var draggedNode in draggedNodes)
                {
                    if (draggedNode.Entity != null && targetNode.Entity != null)
                    {
                        try
                        {
                            // Используем BusinessEntityHelper для изменения визуального родителя
                            await BusinessEntityHelper.ChangeVisualFolderParentForItem(
                                draggedNode.Entity, targetNode.Entity);
                            
                            WebLogger?.Information($"Successfully moved '{draggedNode.Title}' to '{targetNode.Title}'");
                        }
                        catch (InvalidOperationException cyclicEx)
                        {
                            // Специальная обработка циклических зависимостей
                            WebLogger?.Warning($"Cyclic dependency prevented: {cyclicEx.Message}");
                            // Не перезагружаем дерево при ошибке циклической зависимости
                            await RemoveDragTooltip();
                            ClearDraggingFlags();
                            await InvokeAsync(StateHasChanged);
                            return;
                        }
                        catch (Exception moveEx)
                        {
                            WebLogger?.Error($"Failed to move '{draggedNode.Title}' to '{targetNode.Title}': {moveEx.Message}");
                        }
                    }
                }

                // Обновляем дерево после перемещения
                await DrawTreeForCurrentSpace();

                // Сбрасываем выделение со всех элементов
                await ClearAllSelections();

                // Выделяем целевой элемент (тот, в который было выполнено перетаскивание)
                await SelectNodeAfterTreeRefresh(targetNode.Entity?.Id);

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
                
                // Удаляем всплывающий элемент
                await RemoveDragTooltip();
                
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

        /// <summary>
        /// Создает всплывающий элемент с именами перетаскиваемых элементов
        /// </summary>
        private async Task CreateDragTooltip()
        {
            try
            {
                var draggedTitles = SelectedNodes.Where(n => n.IsDragging).Select(n => n.Title).ToList();
                if (!draggedTitles.Any()) return;

                var tooltipContent = string.Join("<br/>", draggedTitles);
                
                await JSRuntime.InvokeVoidAsync("createDragTooltip", tooltipContent);
            }
            catch (Exception ex)
            {
                WebLogger?.Error($"Error creating drag tooltip: {ex.Message}");
            }
        }

        /// <summary>
        /// Удаляет всплывающий элемент
        /// </summary>
        private async Task RemoveDragTooltip()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("removeDragTooltip");
            }
            catch (Exception ex)
            {
                WebLogger?.Error($"Error removing drag tooltip: {ex.Message}");
            }
        }

        /// <summary>
        /// Выделяет узел с указанным ID после обновления дерева
        /// </summary>
        private async Task SelectNodeAfterTreeRefresh(Guid? entityId)
        {
            if (entityId == null) return;

            try
            {
                // Ищем узел с указанным ID в обновленном дереве
                var nodeToSelect = FindNodeByEntityId(TreeData, entityId.Value);
                if (nodeToSelect != null)
                {
                    // Выделяем найденный узел
                    await OnNodeClicked(nodeToSelect);
                    WebLogger?.Information($"Selected target node after drag: '{nodeToSelect.Title}'");
                }
                else
                {
                    WebLogger?.Warning($"Could not find node with ID {entityId} to select after drag");
                }
            }
            catch (Exception ex)
            {
                WebLogger?.Error($"Error selecting node after drag: {ex.Message}");
            }
        }

        /// <summary>
        /// Рекурсивно ищет узел по ID сущности
        /// </summary>
        private TreeNodeItemViewModelBase? FindNodeByEntityId(IEnumerable<TreeNodeItemViewModelBase> nodes, Guid entityId)
        {
            foreach (var node in nodes)
            {
                if (node.Entity?.Id == entityId)
                {
                    return node;
                }

                if (node.Children != null)
                {
                    var found = FindNodeByEntityId(node.Children.Cast<TreeNodeItemViewModelBase>(), entityId);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
        #endregion

        /// <summary>
        /// Обработчик запроса на переименование сущности
        /// </summary>
        private async Task<bool> OnEntityRenameRequestedAsync(TreeNodeItemViewModelBase nodeToRename, string currentName)
        {
            if (nodeToRename?.Entity == null) return false;

            // Переключаем узел в режим редактирования
            EditingNode = nodeToRename;
            EditingText = currentName;

            // Обновляем UI для показа поля ввода
            await InvokeAsync(StateHasChanged);
            // Даем время браузеру отрендерить input, затем фокусируемся
            await Task.Delay(50);
            await editingInput.FocusAsync();
            return true;
        }

        /// <summary>
        /// Завершает inline-редактирование с сохранением
        /// </summary>
        private async Task FinishEditingAsync(bool save = true)
        {
            try
            {
                if (EditingNode == null)
                    return;

                // Сбрасываем ошибки валидации
                HasValidationError = false;
                ValidationErrorMessage = string.Empty;

                if (save && !string.IsNullOrWhiteSpace(EditingText))
                {
                    var entityNameValidator = new EntityNameValidator(WebLogger);
                    var newName= EditingText;

                    // Валидация через EntityNameValidator
                    if (entityNameValidator.IsValidEntityName(newName))
                    {
                        WebLogger?.Information($"EntityNameValidator.IsValidEntityName=ОК EditingNode.Entity={EditingNode.Entity} newName={newName} EditingNode.Title={EditingNode.Title}");
                        if (newName != EditingNode.Title)
                        {
                            // Переименовываем сущность через BusinessEntityHelper
                            try
                            {
                                if (EditingNode.Entity != null)
                                {
                                    WebLogger?.Information($"Renaming entity");

                                    var renamedEntity = await BusinessEntityHelper.RenameEntity(EditingNode.Entity.Id, newName);

                                    if (renamedEntity != null)
                                    {
                                        WebLogger?.Information($"notnull");
                                        // Обновляем имя в узле дерева
                                        EditingNode.Title = newName;
                                        EditingNode.Entity.Name = newName;
                                        WebLogger?.Information($"Successfully renamed entity to '{newName}'");
                                    }
                                    else
                                    {
                                        WebLogger?.Error($"Failed to rename entity to '{newName}'");
                                        HasValidationError = true;
                                        ValidationErrorMessage = "Не удалось сохранить изменения";
                                        await InvokeAsync(StateHasChanged);
                                        return; // Не выходим из режима редактирования при ошибке
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                WebLogger?.Error($"Error renaming entity: {ex.Message}");
                                HasValidationError = true;
                                ValidationErrorMessage = "Ошибка при сохранении";
                                await InvokeAsync(StateHasChanged);
                                return; // Не выходим из режима редактирования при ошибке
                            }
                        }
                    }
                    else
                    {
                        // Показываем ошибку валидации
                        HasValidationError = true;
                        ValidationErrorMessage = "Имя должно содержать только буквы, цифры, пробелы, _ и -, и включать хотя бы одну букву";
                        WebLogger?.Warning($"EntityNameValidator.IsValidEntityName=FAIL; Invalid entity name: '{EditingText}'. {ValidationErrorMessage}");
                        await InvokeAsync(StateHasChanged);
                        return; // Не выходим из режима редактирования при ошибке валидации
                    }
                }
                
                // Выходим из режима редактирования только если нет ошибок
                EditingNode = null;
                EditingText = string.Empty;
                HasValidationError = false;
                ValidationErrorMessage = string.Empty;

                // Обновляем UI
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                WebLogger?.Error($"Error in FinishEditingAsync: {ex.Message}");
                HasValidationError = true;
                ValidationErrorMessage = "Произошла ошибка";
                await InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// Обработчик нажатия клавиш в поле редактирования
        /// </summary>
        private async Task OnEditKeyDownAsync(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await FinishEditingAsync(true);
            }
            else if (e.Key == "Escape")
            {
                await FinishEditingAsync(false);
            }
        }




    }
}