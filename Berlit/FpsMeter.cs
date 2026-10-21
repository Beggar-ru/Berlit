using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Berlit
{
    /// <summary>
    /// FPS активного приложения по событиям Present провайдера Microsoft-Windows-DXGI
    /// — тот же принцип, что у PresentMon: считаем предъявления кадров и делим на время.
    ///
    /// ETW-сессию может открыть только процесс с правами администратора, поэтому
    /// без повышения прав счётчик недоступен и оверлей честно показывает прочерк.
    /// </summary>
    internal sealed class FpsMeter : IDisposable
    {
        private static readonly Guid DxgiProvider = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
        private const int PresentEventId = 42;

        private readonly object _sync = new object();
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        private TraceEventSession _session;
        private Thread _thread;
        private DateTime _windowStart = DateTime.UtcNow;
        private double _fps;
        private int _pid;

        public bool Available { get; private set; }

        public string Status { get; private set; } = "not started";

        public double Fps
        {
            get { lock (_sync) return _fps; }
        }

        public int ForegroundPid
        {
            get { lock (_sync) return _pid; }
        }

        public void Start()
        {
            if (Available) return;

            try
            {
                _session = new TraceEventSession("BerlitFps-" + Process.GetCurrentProcess().Id,
                    TraceEventSessionOptions.Create);
                _session.EnableProvider(DxgiProvider, TraceEventLevel.Verbose, 0x1);
                _session.Source.Dynamic.All += OnEvent;

                _thread = new Thread(ProcessEvents) { IsBackground = true, Name = "BerlitFps" };
                _thread.Start();

                Available = true;
                Status = "etw";
            }
            catch (Exception ex)
            {
                Available = false;
                Status = Shorten(ex.Message);
                DisposeSession();
            }
        }

        private void ProcessEvents()
        {
            try
            {
                _session.Source.Process();
            }
            catch
            {
            }
        }

        private void OnEvent(TraceEvent data)
        {
            if (data == null || (int)data.ID != PresentEventId) return;
            RecordPresent(data.ProcessID);
        }

        /// <summary>Учёт одного предъявления кадра. Вынесено отдельно ради тестируемости математики.</summary>
        internal void RecordPresent(int pid)
        {
            lock (_sync)
            {
                int count;
                _counts.TryGetValue(pid, out count);
                _counts[pid] = count + 1;
            }
        }

        /// <summary>Раз в секунду: FPS активного процесса за прошедший интервал.</summary>
        public void Tick()
        {
            if (!Available) return;

            int foreground = GetForegroundProcessId();

            lock (_sync)
            {
                double seconds = (DateTime.UtcNow - _windowStart).TotalSeconds;
                _windowStart = DateTime.UtcNow;

                int count;
                _counts.TryGetValue(foreground, out count);
                _fps = seconds > 0.2 ? count / seconds : 0;
                _pid = foreground;
                _counts.Clear();
            }
        }

        private static int GetForegroundProcessId()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return 0;

                int pid;
                GetWindowThreadProcessId(window, out pid);
                return pid;
            }
            catch
            {
                return 0;
            }
        }

        private void DisposeSession()
        {
            try
            {
                if (_session != null)
                {
                    _session.Source.StopProcessing();
                    _session.Dispose();
                }
            }
            catch
            {
            }

            _session = null;
            _thread = null;
        }

        public void Dispose()
        {
            Available = false;
            DisposeSession();
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return "unavailable";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 90 ? text.Substring(0, 90) : text;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
    }
}
