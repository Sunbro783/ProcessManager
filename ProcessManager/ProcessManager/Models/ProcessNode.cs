using System.Collections.ObjectModel;

namespace ProcessManager.Models
{
    public class ProcessNode
    {
        public ProcessInfo Process { get; set; }
        public ObservableCollection<ProcessNode> Children { get; set; }
            = new ObservableCollection<ProcessNode>();
    }
}