using Radzen;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Models
{
    public class DocumentTreeNodeItemViewModel : TreeNodeItemViewModelBase
    {
        public DocumentTreeNodeItemViewModel(IWebLoggerService? webLogger = null) : base(webLogger)
        {
        }

        public override string MenuText => "Документ";
        public override string MenuIcon => "description";        public override List<ContextMenuItem> CreateContextMenu()
        {
            return new List<ContextMenuItem>()
            {
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
                case "Rename":
                    await OnRenameAsync();
                    break;
                case "DeleteToTrash":
                    await OnDeleteToTrashAsync();
                    break;
                default:
                    // Логируем неизвестное действие, если есть логгер
                    if (_webLogger != null)
                        await _webLogger.Warning($"Unknown document action: {action}");
                    break;
            }        }

        // Заглушки-обработчики для действий с документами
        private async Task OnRenameAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Переименование документа: {Title}");
        }

        private async Task OnDeleteToTrashAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Удаление документа в корзину: {Title}");
        }
    }
}
