using BusinessEntity.Core.Classes;

namespace BusinessEntity.Models
{
    public class TreeNodeItemViewModelBase
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public BusinessEntity.Core.Classes.BusinessEntity? Entity { get; set; }
        public IEnumerable<TreeNodeItemViewModelBase>? Children { get; set; }
        public bool Expanded { get; set; } = false;
        public string EntityType { get; set; } = string.Empty;
    }
} 