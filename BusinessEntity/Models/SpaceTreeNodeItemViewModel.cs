using Radzen;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.Models
{
    public class SpaceTreeNodeItemViewModel : TreeNodeItemViewModelBase
    {
        public SpaceTreeNodeItemViewModel(IWebLoggerService? webLogger = null) : base(webLogger)
        {
        }

        public override string MenuText => "Пространство";
        public override string MenuIcon => "folder";

        public override List<ContextMenuItem> CreateContextMenu()
        {
            return new List<ContextMenuItem>()
            {
                new ContextMenuItem() 
                { 
                    Text = "Создать папку", 
                    Value = "CreateFolder", 
                    Icon = "create_new_folder"
                },
                new ContextMenuItem() 
                { 
                    Text = "Создать документ", 
                    Value = "CreateDocument", 
                    Icon = "description"
                }
            };
        }

        public override async Task HandleMenuActionAsync(string action)
        {
            switch (action)
            {
                case "CreateFolder":
                    await OnCreateFolderAsync();
                    break;
                case "CreateDocument":
                    await OnCreateDocumentAsync();
                    break;
                default:
                    // Логируем неизвестное действие, если есть логгер
                    if (_webLogger != null)
                        await _webLogger.Warning($"Unknown space action: {action}");
                    break;
            }
        }

        // Заглушки-обработчики для действий с пространством
        private async Task OnCreateFolderAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Создание новой папки в пространстве: {Title}");
            
            // Вызываем обратный вызов для создания папки
            if (OnEntityCreateRequested != null)
                await OnEntityCreateRequested(this, "Folder");
        }

        private async Task OnCreateDocumentAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Создание нового документа в пространстве: {Title}");
            
            // Вызываем обратный вызов для создания документа
            if (OnEntityCreateRequested != null)
                await OnEntityCreateRequested(this, "Document");
        }
    }
}
