using Radzen;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Models
{
    public class FolderTreeNodeItemViewModel : TreeNodeItemViewModelBase
    {
        public FolderTreeNodeItemViewModel(IWebLoggerService? webLogger = null) : base(webLogger)
        {
        }

        public override string MenuText => "Папка";
        public override string MenuIcon => "folder";        public override List<ContextMenuItem> CreateContextMenu()
        {
            return new List<ContextMenuItem>()
            {
                new ContextMenuItem() 
                { 
                    Text = "Создать документ", 
                    Value = "CreateDocument", 
                    Icon = "note_add"
                },
                new ContextMenuItem() 
                { 
                    Text = "Переименовать", 
                    Value = "Rename", 
                    Icon = "edit"
                },
                new ContextMenuItem() 
                { 
                    Text = "Удалить в корзину", 
                    Value = "DeleteToTrash", 
                    Icon = "delete"
                }
            };
        }        public override async Task HandleMenuActionAsync(string action)
        {
            switch (action)
            {
                case "CreateDocument":
                    await OnCreateDocumentAsync();
                    break;
                case "Rename":
                    await OnRenameAsync();
                    break;
                case "DeleteToTrash":
                    await OnDeleteToTrashAsync();
                    break;
                default:
                    // Логируем неизвестное действие, если есть логгер
                    if (_webLogger != null)
                        await _webLogger.Warning($"Unknown folder action: {action}");
                    break;
            }
        }        // Заглушки-обработчики для действий с папками
        private async Task OnCreateDocumentAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Создание нового документа в папке: {Title}");
        }

        private async Task OnRenameAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Переименование папки: {Title}");
        }

        private async Task OnDeleteToTrashAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Удаление папки в корзину: {Title}");
        }
    }
}
