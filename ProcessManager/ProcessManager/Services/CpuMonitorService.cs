using System.Diagnostics;

namespace ProcessManager.Services
{
    public class CpuMonitorService
    {
        private PerformanceCounter[] _counters;

        public CpuMonitorService()
        {
            int cores = System.Environment.ProcessorCount;
            _counters = new PerformanceCounter[cores];

            for (int i = 0; i < cores; i++)
            {
                _counters[i] = new PerformanceCounter(
                    "Processor", "% Processor Time", i.ToString());
                _counters[i].NextValue();
            }
        }

        public float[] GetCpuUsage()
        {
            float[] values = new float[_counters.Length];
            for (int i = 0; i < _counters.Length; i++)
                values[i] = _counters[i].NextValue();
            return values;
        }
    }
}