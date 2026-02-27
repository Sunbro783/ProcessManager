using LiveCharts;
using LiveCharts.Wpf;
using ProcessManager.Models;
using ProcessManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Management;

namespace ProcessManager.ViewModels
{

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessService _service = new ProcessService();
        private readonly CpuMonitorService _cpuService = new CpuMonitorService();
        private readonly DispatcherTimer _timer;


        private int _refreshInterval = 3; // секунды

        public int RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                _refreshInterval = value;
                OnPropertyChanged(nameof(RefreshInterval));

                _timer.Interval = TimeSpan.FromSeconds(_refreshInterval);
            }
        }

        public ObservableCollection<CpuCoreInfo> CpuCores { get; set; }
    = new ObservableCollection<CpuCoreInfo>();

        public ObservableCollection<ProcessInfo> Processes { get; set; }
            = new ObservableCollection<ProcessInfo>();

        public ObservableCollection<ThreadInfo> Threads { get; set; }
            = new ObservableCollection<ThreadInfo>();

        public ObservableCollection<ProcessNode> ProcessTree { get; set; }
            = new ObservableCollection<ProcessNode>();

        public AxesCollection CpuAxisX { get; set; }
        public AxesCollection CpuAxisY { get; set; }

        public SeriesCollection CpuSeries { get; set; }
        public SeriesCollection MemorySeries { get; set; }

        public Array PriorityValues =>
            Enum.GetValues(typeof(ProcessPriorityClass));

        public List<int> RefreshIntervals { get; } =
    new List<int> { 3, 5, 10, 15, 30, 45, 60 };

        public RelayCommand RefreshCommand { get; }
        public RelayCommand KillCommand { get; }
        public RelayCommand ChangePriorityCommand { get; }
        public RelayCommand ApplyAffinityCommand { get; }




        private bool _onlyGUI;
        public bool OnlyGUI
        {
            get => _onlyGUI;
            set
            {
                _onlyGUI = value;
                OnPropertyChanged(nameof(OnlyGUI));
                _ = Refresh();
            }
        }

        private bool _onlySystem;
        public bool OnlySystem
        {
            get => _onlySystem;
            set
            {
                _onlySystem = value;
                OnPropertyChanged(nameof(OnlySystem));
                _ = Refresh();
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                _ = Refresh();
            }
        }

        private ProcessInfo _selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                _selectedProcess = value;
                OnPropertyChanged(nameof(SelectedProcess));

                KillCommand.RaiseCanExecuteChanged();
                ChangePriorityCommand.RaiseCanExecuteChanged();
                ApplyAffinityCommand.RaiseCanExecuteChanged();

                if (_selectedProcess != null)
                {
                    LoadThreads();
                    LoadAffinity();
                    HighlightParent();
                }
                else
                {
                    CpuCores.Clear();   // 👈 галочки исчезают
                }
            }
        }

        private string _affinityHex;
        public string AffinityHex
        {
            get => _affinityHex;
            set { _affinityHex = value; OnPropertyChanged(nameof(AffinityHex)); }
        }

        private string _affinityBinary;
        public string AffinityBinary
        {
            get => _affinityBinary;
            set { _affinityBinary = value; OnPropertyChanged(nameof(AffinityBinary)); }
        }

        public MainViewModel()
        {
            InitCpuCores();
            InitCharts();

            RefreshCommand = new RelayCommand(async _ => await Refresh());
            KillCommand = new RelayCommand(_ => Kill(), _ => SelectedProcess != null);
            ChangePriorityCommand = new RelayCommand(_ => ChangePriority(), _ => SelectedProcess != null);
            ApplyAffinityCommand = new RelayCommand(_ => ApplyAffinity(), _ => SelectedProcess != null);


            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(RefreshInterval);
            _timer.Tick += async (s, e) => await Refresh();
            _timer.Start();

            Task.Run(async () => await Refresh());
        }

        private async Task Refresh()
        {
            int? selectedPid = SelectedProcess?.Id;

            var newList = await Task.Run(() =>
            {
                var processes = _service.GetProcesses();

                return processes
                    .Where(p =>
                        (string.IsNullOrEmpty(SearchText) ||
                         p.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) &&
                        (!OnlyGUI || p.HasWindow) &&
                        (!OnlySystem || IsSystemFast(p)))
                    .OrderBy(p => p.Name)
                    .ToList();
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var toRemove = Processes
                    .Where(p => !newList.Any(n => n.Id == p.Id))
                    .ToList();

                foreach (var r in toRemove)
                    Processes.Remove(r);

                foreach (var existing in Processes)
                {
                    var updated = newList.FirstOrDefault(n => n.Id == existing.Id);
                    if (updated != null)
                        existing.UpdateFrom(updated);
                }

                var toAdd = newList
                    .Where(n => !Processes.Any(p => p.Id == n.Id));

                foreach (var n in toAdd)
                    Processes.Add(n);

                if (selectedPid.HasValue)
                {
                    var newSelection = Processes
                        .FirstOrDefault(p => p.Id == selectedPid.Value);

                    if (!ReferenceEquals(SelectedProcess, newSelection))
                        SelectedProcess = newSelection;
                }

                BuildTree();
                UpdateMemoryChart();
                UpdateCpuChart();
            });
        }

        private bool IsSystemFast(ProcessInfo p)
        {
            try
            {
                var proc = Process.GetProcessById(p.Id);

                return proc.SessionId == 0
                       || p.Name.Equals("System", StringComparison.OrdinalIgnoreCase)
                       || p.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void Kill()
        {
            try
            {
                _service.Kill(SelectedProcess.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка завершения: " + ex.Message);
            }
        }

        private void ChangePriority()
        {
            try
            {
                if (SelectedProcess.Priority == ProcessPriorityClass.RealTime)
                {
                    var result = MessageBox.Show(
                        "Realtime может вызвать нестабильность системы. Продолжить?",
                        "Предупреждение",
                        MessageBoxButton.YesNo);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                _service.SetPriority(SelectedProcess.Id,
                    SelectedProcess.Priority);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка изменения приоритета: " + ex.Message);
            }
        }

        private void LoadThreads()
        {
            Threads.Clear();
            if (SelectedProcess == null) return;

            foreach (var t in _service.GetThreads(SelectedProcess.Id))
                Threads.Add(t);
        }

        private void InitCpuCores()
        {
            CpuCores.Clear();
        }

        private void LoadAffinity()
        {
            if (SelectedProcess == null)
                return;

            CpuCores.Clear();

            try
            {
                long mask;

                // если уже меняли affinity — используем сохранённую
                if (SelectedProcess.AffinityMask.HasValue)
                {
                    mask = SelectedProcess.AffinityMask.Value;
                }
                else
                {
                    var process = Process.GetProcessById(SelectedProcess.Id);
                    mask = process.ProcessorAffinity.ToInt64();
                    SelectedProcess.AffinityMask = mask;
                }

                int coreCount = Environment.ProcessorCount;

                for (int i = 0; i < coreCount; i++)
                {
                    var core = new CpuCoreInfo
                    {
                        CoreNumber = i,
                        IsEnabled = (mask & (1L << i)) != 0
                    };

                    core.PropertyChanged += CpuCoreChanged;

                    CpuCores.Add(core);
                }
            }
            catch
            {
                CpuCores.Clear();
            }
        }

        private void CpuCoreChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CpuCoreInfo.IsEnabled))
                return;

            ApplyAffinity();
        }

        private void ApplyAffinity()
        {
            if (SelectedProcess == null)
                return;

            try
            {
                long mask = 0;

                foreach (var core in CpuCores)
                {
                    if (core.IsEnabled)
                        mask |= (1L << core.CoreNumber);
                }

                if (mask == 0)
                    return;

                var process = Process.GetProcessById(SelectedProcess.Id);
                process.ProcessorAffinity = (IntPtr)mask;

                // сохраняем маску
                SelectedProcess.AffinityMask = mask;
            }
            catch { }
        }

        private void BuildTree()
        {
            ProcessTree.Clear();

            var dict = Processes.ToDictionary(p => p.Id);
            var nodes = Processes.ToDictionary(
                p => p.Id,
                p => new ProcessNode { Process = p });

            foreach (var node in nodes.Values)
            {
                if (dict.ContainsKey(node.Process.ParentId))
                {
                    nodes[node.Process.ParentId]
                        .Children.Add(node);
                }
                else
                {
                    ProcessTree.Add(node); // корневой
                }
            }
        }

        private ProcessNode BuildNode(ProcessInfo p,
            ILookup<int, ProcessInfo> lookup)
        {
            var node = new ProcessNode { Process = p };
            foreach (var child in lookup[p.Id])
                node.Children.Add(BuildNode(child, lookup));
            return node;
        }

        private void HighlightParent()
        {
            // Можно добавить выделение родителя при необходимости
        }

        private void InitCharts()
        {
            CpuSeries = new SeriesCollection();

            CpuAxisX = new AxesCollection
{
    new Axis
    {
        Title = "Ядра",
        Labels = Enumerable.Range(0, Environment.ProcessorCount)
                           .Select(i => i.ToString())
                           .ToArray(),

        Separator = new Separator
        {
            Step = 1
        },

        LabelsRotation = 0,
        FontSize = 10
    }
};

            CpuAxisY = new AxesCollection
    {
        new Axis
        {
            Title = "Загрузка (%)",
            MinValue = 0,
            MaxValue = 100,
            LabelFormatter = value => value + " %"
        }
    };

            MemorySeries = new SeriesCollection();
        }

        private void UpdateCpuChart()
        {
            var usage = _cpuService.GetCpuUsage();

            CpuSeries.Clear();

            CpuSeries.Add(new ColumnSeries
            {
                Title = "CPU",
                Values = new ChartValues<float>(usage),
                DataLabels = false   // ❗ убираем числа на столбиках
            });
        }

        private bool IsSystemProcess(ProcessInfo p)
        {
            try
            {
                var proc = Process.GetProcessById(p.Id);
                return proc.SessionId == 0;
            }
            catch
            {
                return false;
            }
        }

        private int GetPhysicalCoreCount()
        {
            try
            {
                using (var searcher =
                    new System.Management.ManagementObjectSearcher(
                        "select NumberOfCores from Win32_Processor"))
                {
                    int cores = 0;

                    foreach (var item in searcher.Get())
                        cores += int.Parse(item["NumberOfCores"].ToString());

                    return cores;
                }
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }
        private void UpdateMemoryChart()
        {
            MemorySeries.Clear();

            var top10 = Processes
                .OrderByDescending(p => p.MemoryMB)
                .Take(10);

            foreach (var p in top10)
            {
                MemorySeries.Add(new PieSeries
                {
                    Title = p.Name,
                    Values = new ChartValues<double> { p.MemoryMB },

                    DataLabels = true,
                    LabelPoint = chartPoint =>
                        $"{chartPoint.Y:F1} MB",   // ❗ только число + MB

                    ToolTip = new DefaultTooltip()
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(prop));


    }



    public class CpuCoreViewModel : INotifyPropertyChanged
    {
        public int CoreNumber { get; set; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(prop));
    }






}