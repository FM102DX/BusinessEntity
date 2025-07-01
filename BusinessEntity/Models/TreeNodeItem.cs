namespace BusinessEntity.Models
{
    public class TreeNodeItem
    {
        public string Title { get; set; } = string.Empty;
        public IEnumerable<TreeNodeItem>? Children { get; set; }
    }
} 