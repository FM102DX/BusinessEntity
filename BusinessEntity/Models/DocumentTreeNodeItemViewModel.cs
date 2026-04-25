using Radzen;
using BusinessEntity.WebLogger.Services;

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
                    Text = "Открыть",
                    Value = "Open",
                    Icon = "open_in_new"
                },
                new ContextMenuItem()
                {
                    Text = "Редактировать",
                    Value = "Edit",
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
                case "Open":
                    await OnOpenAsync();
                    break;
                case "Edit":
                    await OnEditAsync();
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

        private async Task OnOpenAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Открытие документа: {Title}");

            if (OnEntityOpenRequested != null)
                await OnEntityOpenRequested(this);
        }

        private async Task OnEditAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Открытие документа в режиме редактирования: {Title}");

            if (OnEntityOpenForEditRequested != null)
                await OnEntityOpenForEditRequested(this);
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
