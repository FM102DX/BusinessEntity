using BusinessEntity.Models;

namespace BusinessEntity.Services
{
    public interface ITreeSelectionService
    {
        List<TreeNodeItemViewModelBase> SelectedNodes { get; }
        TreeNodeItemViewModelBase? SingleSelectedNode { get; }
        bool IsMultiSelectActive { get; }
        
        event Action<List<TreeNodeItemViewModelBase>>? SelectionChanged;
        
        void SetSelectedNodes(List<TreeNodeItemViewModelBase> nodes);
        void ClearSelection();
        string GetSelectedNodesInfo();
    }

    public class TreeSelectionService : ITreeSelectionService
    {
        private List<TreeNodeItemViewModelBase> _selectedNodes = new List<TreeNodeItemViewModelBase>();
        
        public List<TreeNodeItemViewModelBase> SelectedNodes => new List<TreeNodeItemViewModelBase>(_selectedNodes);
        
        public TreeNodeItemViewModelBase? SingleSelectedNode => 
            _selectedNodes.Count == 1 ? _selectedNodes.First() : null;
        
        public bool IsMultiSelectActive => _selectedNodes.Count > 1;
        
        public event Action<List<TreeNodeItemViewModelBase>>? SelectionChanged;

        public void SetSelectedNodes(List<TreeNodeItemViewModelBase> nodes)
        {
            _selectedNodes = new List<TreeNodeItemViewModelBase>(nodes ?? new List<TreeNodeItemViewModelBase>());
            SelectionChanged?.Invoke(SelectedNodes);
        }

        public void ClearSelection()
        {
            _selectedNodes.Clear();
            SelectionChanged?.Invoke(SelectedNodes);
        }

        public string GetSelectedNodesInfo()
        {
            if (!IsMultiSelectActive)
            {
                return SingleSelectedNode != null ? $"Выбран: {SingleSelectedNode.Title}" : "Ничего не выбрано";
            }
            
            var folders = _selectedNodes.Count(n => n.EntityType == "Folder");
            var documents = _selectedNodes.Count(n => n.EntityType == "Document");
            var spaces = _selectedNodes.Count(n => n.EntityType == "Space");
            
            var info = new List<string>();
            if (folders > 0) info.Add($"папок: {folders}");
            if (documents > 0) info.Add($"документов: {documents}");
            if (spaces > 0) info.Add($"пространств: {spaces}");
            
            return $"Выбрано {_selectedNodes.Count} элементов ({string.Join(", ", info)})";
        }
    }
} 