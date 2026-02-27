using System.ComponentModel;

namespace ProcessManager.Models
{
    public class CpuCoreInfo : INotifyPropertyChanged
    {
        private bool _isEnabled;

        public int CoreNumber { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}