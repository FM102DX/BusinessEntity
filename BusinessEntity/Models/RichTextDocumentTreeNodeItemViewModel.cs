using Radzen;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.Models
{
    // Узел дерева для rich-text документа.
    // Пока поддерживает только просмотр и удаление, без режима inline-редактирования содержимого.
    public class RichTextDocumentTreeNodeItemViewModel : TreeNodeItemViewModelBase
    {
        public RichTextDocumentTreeNodeItemViewModel(IWebLoggerService? webLogger = null) : base(webLogger)
        {
        }

        public override string MenuText => "Рич-документ";
        public override string MenuIcon => "article";

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
                case "Delete":
                    await OnDeleteAsync();
                    break;
                default:
                    if (_webLogger != null)
                        await _webLogger.Warning($"Unknown rich-text document action: {action}");
                    break;
            }
        }

        private async Task OnOpenAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Открытие rich-text документа: {Title}");

            if (OnEntityOpenRequested != null)
                await OnEntityOpenRequested(this);
        }

        private async Task OnDeleteAsync()
        {
            if (_webLogger != null)
                await _webLogger.Information($"Запрос на удаление rich-text документа: {Title}");

            if (OnEntityDeleteRequested != null)
                await OnEntityDeleteRequested(this);
        }
    }
}
