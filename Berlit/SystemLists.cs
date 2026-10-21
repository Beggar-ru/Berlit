using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.ServiceProcess;

namespace Berlit
{
    internal sealed class ServiceEntry
    {
        public string DisplayName = string.Empty;
        public string Name = string.Empty;
        public string Status = string.Empty;
        public string StartMode = string.Empty;

        public bool IsRunning
        {
            get { return string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase); }
        }
    }

    internal sealed class ProcessEntry
    {
        public string Name = string.Empty;
        public int Pid;
        public double CpuPercent;
        public long WorkingSet;
        public int Threads;
        public int Handles;
        public string Started = string.Empty;
    }

    /// <summary>
    /// Списки служб и процессов. Службы читаются из SCM через ServiceController,
    /// процессы — через System.Diagnostics. Загрузка CPU процесса считается как
    /// дельта процессорного времени между двумя опросами (как в диспетчере задач).
    /// </summary>
    internal static class SystemLists
    {
        public static List<ServiceEntry> GetServices()
        {
            var result = new List<ServiceEntry>();
            ServiceController[] services;
            try
            {
                services = ServiceController.GetServices();
            }
            catch
            {
                return result;
            }

            foreach (ServiceController service in services)
            {
                try
                {
                    var entry = new ServiceEntry
                    {
                        Name = service.ServiceName,
                        DisplayName = service.DisplayName
                    };

                    try { entry.Status = service.Status.ToString(); }
                    catch { entry.Status = string.Empty; }

                    try { entry.StartMode = service.StartType.ToString(); }
                    catch { entry.StartMode = string.Empty; }

                    result.Add(entry);
                }
                catch
                {
                    // Служба удалена между перечислением и чтением — пропускаем.
                }
                finally
                {
                    try { service.Dispose(); }
                    catch { }
                }
            }

            result.Sort(delegate (ServiceEntry a, ServiceEntry b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }
    }

    /// <summary>
    /// Считает загрузку CPU по процессам. Хранит предыдущие значения
    /// TotalProcessorTime, поэтому первый опрос после старта даёт 0 %.
    /// </summary>
    internal sealed class ProcessCpuTracker
    {
        private readonly Dictionary<int, TimeSpan> _previous = new Dictionary<int, TimeSpan>();
        private DateTime _stamp = DateTime.MinValue;

        public List<ProcessEntry> Sample()
        {
            DateTime now = DateTime.UtcNow;
            double elapsed = _stamp == DateTime.MinValue ? 0 : (now - _stamp).TotalSeconds;
            _stamp = now;

            var result = new List<ProcessEntry>();
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return result;
            }

            int cores = Math.Max(1, Environment.ProcessorCount);
            var alive = new HashSet<int>();

            foreach (Process process in processes)
            {
                try
                {
                    int pid = process.Id;
                    alive.Add(pid);

                    var entry = new ProcessEntry();
                    entry.Pid = pid;

                    try { entry.Name = process.ProcessName; }
                    catch { entry.Name = "pid " + pid.ToString(CultureInfo.InvariantCulture); }

                    try { entry.WorkingSet = process.WorkingSet64; }
                    catch { }

                    try { entry.Threads = process.Threads.Count; }
                    catch { }

                    try { entry.Handles = process.HandleCount; }
                    catch { }

                    try
                    {
                        TimeSpan cpu = process.TotalProcessorTime;
                        TimeSpan previous;
                        if (elapsed > 0.2 && _previous.TryGetValue(pid, out previous))
                        {
                            double percent = (cpu - previous).TotalSeconds / elapsed / cores * 100.0;
                            if (percent >= 0 && percent <= 100) entry.CpuPercent = percent;
                        }
                        _previous[pid] = cpu;
                    }
                    catch
                    {
                        // Защищённые процессы не отдают время — останется 0 %.
                    }

                    try
                    {
                        DateTime started = process.StartTime;
                        entry.Started = started.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                    }

                    result.Add(entry);
                }
                catch
                {
                }
                finally
                {
                    try { process.Dispose(); }
                    catch { }
                }
            }

            // Чистим кэш от завершившихся процессов, иначе он растёт бесконечно.
            if (_previous.Count > alive.Count * 2 + 64)
            {
                var stale = new List<int>();
                foreach (int pid in _previous.Keys)
                    if (!alive.Contains(pid)) stale.Add(pid);
                foreach (int pid in stale) _previous.Remove(pid);
            }

            return result;
        }
    }
}
