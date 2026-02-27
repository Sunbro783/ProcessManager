using ProcessManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace ProcessManager.Services
{

    public class ProcessService
    {
        public List<ProcessInfo> GetProcesses()
        {
            var result = new List<ProcessInfo>();

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    result.Add(new ProcessInfo
                    {
                        Id = p.Id,
                        Name = p.ProcessName,
                        Priority = p.PriorityClass,
                        MemoryMB = Math.Round(p.WorkingSet64 / 1024.0 / 1024, 2),
                        CpuTime = p.TotalProcessorTime,
                        ThreadCount = p.Threads.Count,
                        ParentId = GetParentId(p.Id),
                        HasWindow = p.MainWindowHandle != IntPtr.Zero
                    });
                }
                catch { }
            }

            return result;
        }

        public List<ThreadInfo> GetThreads(int pid)
        {
            var list = new List<ThreadInfo>();

            try
            {
                var p = Process.GetProcessById(pid);
                foreach (ProcessThread t in p.Threads)
                {
                    list.Add(new ThreadInfo
                    {
                        Id = t.Id,
                        Priority = t.PriorityLevel,
                        State = t.ThreadState,
                        ProcessorTime = t.TotalProcessorTime
                    });
                }
            }
            catch { }

            return list;
        }

        public void Kill(int pid) =>
            Process.GetProcessById(pid).Kill();

        public void SetPriority(int pid, ProcessPriorityClass priority) =>
            Process.GetProcessById(pid).PriorityClass = priority;

        public IntPtr GetAffinity(int pid) =>
            Process.GetProcessById(pid).ProcessorAffinity;

        public void SetAffinity(int pid, IntPtr mask) =>
            Process.GetProcessById(pid).ProcessorAffinity = mask;

        private int GetParentId(int pid)
        {
            using (var query = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={pid}"))
            {
                var obj = query.Get().Cast<ManagementObject>().FirstOrDefault();
                return obj != null ? Convert.ToInt32(obj["ParentProcessId"]) : 0;
            }
        }

        public bool IsSystemProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);

                // 1. Большинство системных процессов в Session 0
                if (process.SessionId == 0)
                    return true;

                // 2. Проверяем владельца через WMI
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Process WHERE ProcessId={pid}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object[] args = new object[2];
                        int result = Convert.ToInt32(obj.InvokeMethod("GetOwner", args));

                        if (result == 0)
                        {
                            string user = args[0]?.ToString();
                            if (user == null) return false;

                            return user.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase)
                                || user.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase)
                                || user.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}