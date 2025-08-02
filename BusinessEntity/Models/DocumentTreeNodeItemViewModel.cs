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
        public override string MenuIcon => "description";        
        public override List<ContextMenuItem> CreateContextMenu()
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
                    Text = "Удалить", 
                    Value = "Delete", 
                    Icon = "delete"
                }
            };
        }        
        public override async Task HandleMenuActionAsync(string action)
        {
            switch (action)
            {
                case "Rename":
                    await OnRenameAsync();
                    break;
                case "Delete":
                    await OnDeleteAsync();
                    break;
                default:
                    // Логируем неизвестное действие, если есть логгер
                    if (_webLogger != null)
                        await _webLogger.Warning($"Unknown document action: {action}");
                    break;
            }
        }

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

        private async Task OnDeleteAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Запрос на удаление документа: {Title}");
            
            // Вызываем обратный вызов для удаления через TreeComponent
            if (OnEntityDeleteRequested != null)
                await OnEntityDeleteRequested(this);
        }
    }
}
