using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ProcessManager.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private ProcessPriorityClass _priority;
        private double _memoryMB;
        private TimeSpan _cpuTime;
        private int _threadCount;

        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
        public bool HasWindow { get; set; }
        public long? AffinityMask { get; set; }

        public ProcessPriorityClass Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }

        public double MemoryMB
        {
            get => _memoryMB;
            set
            {
                if (_memoryMB != value)
                {
                    _memoryMB = value;
                    OnPropertyChanged(nameof(MemoryMB));
                }
            }
        }

        public TimeSpan CpuTime
        {
            get => _cpuTime;
            set
            {
                if (_cpuTime != value)
                {
                    _cpuTime = value;
                    OnPropertyChanged(nameof(CpuTime));
                }
            }
        }

        public int ThreadCount
        {
            get => _threadCount;
            set
            {
                if (_threadCount != value)
                {
                    _threadCount = value;
                    OnPropertyChanged(nameof(ThreadCount));
                }
            }
        }

        // 🔥 Метод обновления без пересоздания объекта
        public void UpdateFrom(ProcessInfo other)
        {
            Priority = other.Priority;
            MemoryMB = other.MemoryMB;
            CpuTime = other.CpuTime;
            ThreadCount = other.ThreadCount;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}