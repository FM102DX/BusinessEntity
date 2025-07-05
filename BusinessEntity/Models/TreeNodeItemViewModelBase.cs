using BusinessEntity.Core.Classes;

namespace BusinessEntity.Models
{
    public class TreeNodeItemViewModelBase
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public BusinessEntity.Core.Classes.BusinessEntity? Entity { get; set; }
        
        // Свойство Children должно быть public и никогда не null для корректной работы с Radzen Tree
        public IEnumerable<TreeNodeItemViewModelBase> Children { get; set; } = new List<TreeNodeItemViewModelBase>();
        
        public bool Expanded { get; set; } = false;
        public string EntityType { get; set; } = string.Empty;
        
        // Вспомогательное свойство для определения наличия дочерних элементов
        public bool HasChildren => Children?.Any() == true;
    }
}