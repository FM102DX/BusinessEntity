using BusinessEntity.Core.Classes;
using Radzen;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Models
{
    public abstract class TreeNodeItemViewModelBase
    {
        protected readonly IWebLoggerService? _webLogger;

        protected TreeNodeItemViewModelBase(IWebLoggerService? webLogger = null)
        {
            _webLogger = webLogger;
        }

        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public BusinessEntity.Core.Classes.BusinessEntity? Entity { get; set; }
        
        // Свойство Children должно быть public и никогда не null для корректной работы с Radzen Tree
        public IEnumerable<TreeNodeItemViewModelBase> Children { get; set; } = new List<TreeNodeItemViewModelBase>();
        
        public bool Expanded { get; set; } = false;
        public string EntityType { get; set; } = string.Empty;
        
        // Вспомогательное свойство для определения наличия дочерних элементов
        public bool HasChildren => Children?.Any() == true;
        
        // Виртуальные свойства для текста и иконки меню
        public virtual string MenuText => "Элемент";
        public virtual string MenuIcon => "help";

        // Делегат для обратного вызова в TreeComponent для создания сущностей
        public Func<TreeNodeItemViewModelBase, string, Task>? OnEntityCreateRequested { get; set; }
        
        // Абстрактный метод для создания контекстного меню
        public abstract List<ContextMenuItem> CreateContextMenu();
        
        // Абстрактный метод для обработки действий меню
        public abstract Task HandleMenuActionAsync(string action);
    }
}