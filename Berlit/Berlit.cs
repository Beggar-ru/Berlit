using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// Главное окно. Вся тяжёлая работа (опрос датчиков, служб, процессов)
    /// выполняется в фоне, в UI-поток возвращаются только готовые снимки.
    /// </summary>
    public partial class Berlit : Form
    {
        private IHardwareProvider _provider;
        private readonly ProcessCpuTracker _cpuTracker = new ProcessCpuTracker();
        private HudForm _overlay;

        private bool _ready;
        private bool _hardwareBusy;
        private bool _servicesBusy;
        private bool _processesBusy;

        private DateTime _hardwareStamp = DateTime.MinValue;
        private DateTime _servicesStamp = DateTime.MinValue;
        private DateTime _processesStamp = DateTime.MinValue;
        private DateTime _infoStamp = DateTime.MinValue;
        private bool _infoLoaded;
        private bool _infoBusy;

        private List<ServiceEntry> _services = new List<ServiceEntry>();
        private List<ProcessEntry> _processes = new List<ProcessEntry>();

        private int _serviceSort = -1;          // -1: порядок по имени службы
        private bool _serviceAscending = true;
        private int _processSort = 2;           // CPU
        private bool _processAscending = false;

        /// <summary>В конструкторе формы DesignMode ещё не выставлен — используем LicenseManager.</summary>
        private static bool IsDesignTime
        {
            get { return LicenseManager.UsageMode == LicenseUsageMode.Designtime; }
        }

        public Berlit()
        {
            InitializeComponent();

            // В дизайнере форму конструирует хост Visual Studio:
            // грузить иконку и подписываться на события ему незачем.
            if (IsDesignTime) return;

            try { Icon = new Icon(typeof(Berlit), "berlit.ico"); }
            catch { }

            dataGridServices.ColumnHeaderClicked += dataGridServices_ColumnHeaderClicked;
            dataGridProcesses.ColumnHeaderClicked += dataGridProcesses_ColumnHeaderClicked;
        }

        // ==================================================================
        // Жизненный цикл
        // ==================================================================

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.ApplyDarkTitleBar(Handle);
        }

        private void Berlit_Load(object sender, EventArgs e)
        {
            ApplyTheme();
            BuildGridColumns();

            try
            {
                _provider = CreateProvider();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось создать провайдер мониторинга:\r\n" + ex.Message,
                    "Berlit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _provider = null;
            }

            _ready = true;

            RefreshHardware();
            RefreshServices();
            RefreshProcesses();

            timerRefresh.Start();
        }

        private static IHardwareProvider CreateProvider()
        {
            // Демо-данные отключены. Чтобы включить их для проверки вёрстки,
            // определите BERLIT_DEMO в Berlit.csproj (см. DemoHardwareProvider.cs).
#if BERLIT_DEMO
            return new DemoHardwareProvider();
#else
            return new SystemHardwareProvider();
#endif
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ready = false;
            timerRefresh.Stop();

            if (_overlay != null)
            {
                try
                {
                    _overlay.Close();
                    _overlay.Dispose();
                }
                catch
                {
                }
                _overlay = null;
            }

            if (_provider != null)
            {
                _provider.Dispose();
                _provider = null;
            }
            base.OnFormClosed(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                RefreshActiveTab(true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.F))
            {
                textBoxSearch.Focus();
                textBoxSearch.SelectAll();
                return true;
            }
            if (keyData == Keys.Escape && textBoxSearch.Focused)
            {
                textBoxSearch.Clear();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ==================================================================
        // Оформление
        // ==================================================================

        private void ApplyTheme()
        {
            SuspendLayout();

            BackColor = Theme.Back;
            ForeColor = Theme.Text;

            panelTop.BackColor = Theme.Back;
            labelTitle.Font = Fonts.Title;
            labelTitle.ForeColor = Theme.Text;
            labelSubtitle.Font = Fonts.Caption;
            labelSubtitle.ForeColor = Theme.TextFaint;

            panelSearch.BackColor = Theme.SurfaceAlt;
            textBoxSearch.BackColor = Theme.SurfaceAlt;
            textBoxSearch.ForeColor = Theme.Text;
            textBoxSearch.Font = Fonts.Small;
            labelSearchHint.BackColor = Theme.SurfaceAlt;
            labelSearchHint.ForeColor = Theme.TextFaint;
            labelSearchHint.Font = Fonts.Small;
            labelSearchHint.BringToFront();

            tabControl1.BackColor = Theme.Back;
            foreach (TabPage page in tabControl1.TabPages)
            {
                page.BackColor = Theme.Surface;
                page.ForeColor = Theme.Text;
            }

            // Внутренние вкладки SYSTEM INFO: без белой рамки вокруг страниц.
            tabInfo.BackColor = Theme.Surface;
            foreach (TabPage page in tabInfo.TabPages)
            {
                page.BackColor = Theme.Surface;
                page.ForeColor = Theme.Text;
            }

            StyleToolbar(panelPcToolbar);
            StyleToolbar(panelServicesToolbar);
            StyleToolbar(panelProcessesToolbar);
            StyleToolbar(panelInfoToolbar);

            StyleButton(buttonExpandAll);
            StyleButton(buttonCollapseAll);
            StyleButton(buttonRefresh);
            StyleButton(buttonOverlay);
            StyleButton(buttonInfoRefresh);
            StyleButton(buttonInfoCopy);

            labelUpdated.Font = Fonts.Small;
            labelUpdated.ForeColor = Theme.TextDim;

            labelInfoSummary.Font = Fonts.Small;
            labelInfoSummary.ForeColor = Theme.TextDim;
            labelInfoUpdated.Font = Fonts.Small;
            labelInfoUpdated.ForeColor = Theme.TextDim;

            labelServicesSummary.Font = Fonts.Small;
            labelServicesSummary.ForeColor = Theme.TextDim;

            labelProcessesSummary.Font = Fonts.Small;
            labelProcessesSummary.ForeColor = Theme.TextDim;

            checkRunningOnly.BackColor = Theme.SurfaceAlt;
            checkRunningOnly.ForeColor = Theme.TextSensor;
            checkRunningOnly.Font = Fonts.Small;

            hwTree.BackColor = Theme.Surface;
            dataGridServices.BackColor = Theme.Surface;
            dataGridProcesses.BackColor = Theme.Surface;

            infoPanelCpu.BackColor = Theme.Surface;
            infoPanelGpu.BackColor = Theme.Surface;
            infoPanelRam.BackColor = Theme.Surface;
            infoPanelMainboard.BackColor = Theme.Surface;
            infoPanelStorage.BackColor = Theme.Surface;

            panelStatus.BackColor = Theme.SurfaceAlt;
            labelStatus.Font = Fonts.Small;
            labelStatus.ForeColor = Theme.TextDim;
            labelSummary.Font = Fonts.Small;
            labelSummary.ForeColor = Theme.TextDim;

            panelTop.Paint += panelTop_Paint;
            panelPcToolbar.Paint += toolbar_Paint;
            panelServicesToolbar.Paint += toolbar_Paint;
            panelProcessesToolbar.Paint += toolbar_Paint;
            panelInfoToolbar.Paint += toolbar_Paint;
            panelStatus.Paint += panelStatus_Paint;

            EnableDoubleBuffer(panelTop);
            EnableDoubleBuffer(panelPcToolbar);
            EnableDoubleBuffer(panelServicesToolbar);
            EnableDoubleBuffer(panelProcessesToolbar);
            EnableDoubleBuffer(panelInfoToolbar);
            EnableDoubleBuffer(panelStatus);
            EnableDoubleBuffer(panelSearch);

            ResumeLayout();
        }

        private static void StyleToolbar(Panel panel)
        {
            panel.BackColor = Theme.SurfaceAlt;
        }

        private static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Theme.Button;
            button.ForeColor = Theme.Text;
            button.Font = Fonts.Small;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.ButtonHot;
            button.FlatAppearance.MouseDownBackColor = Theme.SurfaceAlt;
        }

        private static void EnableDoubleBuffer(Control control)
        {
            try
            {
                PropertyInfo property = typeof(Control).GetProperty("DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (property != null) property.SetValue(control, true, null);
            }
            catch
            {
            }
        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var brush = new SolidBrush(Theme.Accent))
                g.FillRectangle(brush, 14, 16, 3, 20);

            using (var pen = new Pen(Theme.GridLine))
                g.DrawLine(pen, 0, panelTop.Height - 1, panelTop.Width, panelTop.Height - 1);
        }

        private void toolbar_Paint(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;
            using (var pen = new Pen(Theme.GridLine))
                e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
        }

        private void panelStatus_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Theme.GridLine))
                e.Graphics.DrawLine(pen, 0, 0, panelStatus.Width, 0);
        }

        private void panelSearch_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            bool focused = textBoxSearch.Focused;

            using (var brush = new SolidBrush(Theme.SurfaceAlt))
                g.FillRectangle(brush, panelSearch.ClientRectangle);

            using (var pen = new Pen(focused ? Theme.Accent : Theme.Border))
                g.DrawRectangle(pen, 0, 0, panelSearch.Width - 1, panelSearch.Height - 1);

            // Лупа: окружность и ручка. Без иконочных шрифтов и эмодзи.
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(focused ? Theme.Accent : Theme.TextDim, 1.4f))
            {
                g.DrawEllipse(pen, 9.5f, 8.5f, 9f, 9f);
                g.DrawLine(pen, 17.2f, 17.6f, 21.5f, 21.9f);
            }
            g.SmoothingMode = old;
        }

        // ==================================================================
        // Колонки
        // ==================================================================

        // У каждого раздела свой набор и порядок колонок — как в HWMonitor.
        private static readonly HwColumnDef[] SystemColumns =
        {
            new HwColumnDef(HwCol.Info, "Details", 0, HorizontalAlignment.Left)
        };

        private static readonly HwColumnDef[] CpuColumns =
        {
            new HwColumnDef(HwCol.Load, "Load", 78, HorizontalAlignment.Right, false, true),
            new HwColumnDef(HwCol.Temp, "Temp", 70, HorizontalAlignment.Right, true),
            new HwColumnDef(HwCol.TempMin, "Min", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.TempMax, "Max", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Clock, "Core clock", 104, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Voltage, "Voltage", 82, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Power, "Power", 78, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Info, "Info", 0, HorizontalAlignment.Left)
        };

        private static readonly HwColumnDef[] MemoryColumns =
        {
            new HwColumnDef(HwCol.Load, "Load", 78, HorizontalAlignment.Right, false, true),
            new HwColumnDef(HwCol.Info, "Used / total", 0, HorizontalAlignment.Left)
        };

        private static readonly HwColumnDef[] GpuColumns =
        {
            new HwColumnDef(HwCol.Load, "Load", 78, HorizontalAlignment.Right, false, true),
            new HwColumnDef(HwCol.Temp, "Temp", 70, HorizontalAlignment.Right, true),
            new HwColumnDef(HwCol.TempMin, "Min", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.TempMax, "Max", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Clock, "Core clock", 104, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.MemClock, "Mem clock", 104, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.FanRpm, "Fan", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Power, "Power", 78, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Info, "Info", 0, HorizontalAlignment.Left)
        };

        private static readonly HwColumnDef[] StorageColumns =
        {
            new HwColumnDef(HwCol.Load, "Load", 78, HorizontalAlignment.Right, false, true),
            new HwColumnDef(HwCol.Temp, "Temp", 70, HorizontalAlignment.Right, true),
            new HwColumnDef(HwCol.TempMin, "Min", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.TempMax, "Max", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Info, "Info", 0, HorizontalAlignment.Left)
        };

        private static readonly HwColumnDef[] BoardColumns =
        {
            new HwColumnDef(HwCol.Temp, "Temp", 70, HorizontalAlignment.Right, true),
            new HwColumnDef(HwCol.TempMin, "Min", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.TempMax, "Max", 66, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Voltage, "Voltage", 90, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.FanRpm, "Fan", 80, HorizontalAlignment.Right),
            new HwColumnDef(HwCol.Info, "Info", 0, HorizontalAlignment.Left)
        };

        private void BuildGridColumns()
        {
            dataGridServices.SetColumns(new[]
            {
                new GridColumnDef("Service", 2.6f, HorizontalAlignment.Left, 180),
                new GridColumnDef("Internal name", 1.8f, HorizontalAlignment.Left, 130),
                new GridColumnDef("Status", 0.9f, HorizontalAlignment.Left, 90),
                new GridColumnDef("Start type", 1.0f, HorizontalAlignment.Left, 100)
            });

            dataGridProcesses.SetColumns(new[]
            {
                new GridColumnDef("Process", 2.0f, HorizontalAlignment.Left, 140),
                new GridColumnDef("PID", 0.6f, HorizontalAlignment.Right, 60),
                new GridColumnDef("CPU", 0.75f, HorizontalAlignment.Right, 70),
                new GridColumnDef("Memory", 0.95f, HorizontalAlignment.Right, 85),
                new GridColumnDef("Threads", 0.7f, HorizontalAlignment.Right, 70),
                new GridColumnDef("Handles", 0.75f, HorizontalAlignment.Right, 70),
                new GridColumnDef("Started", 0.95f, HorizontalAlignment.Right, 90)
            });
        }

        // ==================================================================
        // Обновление данных
        // ==================================================================

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (!_ready) return;

            // Железо опрашивается всегда: строка состояния с CPU/RAM/GPU должна
            // быть живой на любой вкладке. Тяжёлые списки — только когда видны.
            if ((DateTime.Now - _hardwareStamp).TotalMilliseconds >= 1900) RefreshHardware();

            switch (tabControl1.SelectedIndex)
            {
                case 1:
                    if (!_infoLoaded || (DateTime.Now - _infoStamp).TotalSeconds >= 60) RefreshInfo(false);
                    break;
                case 2:
                    if ((DateTime.Now - _servicesStamp).TotalMilliseconds >= 7000) RefreshServices();
                    break;
                case 3:
                    if ((DateTime.Now - _processesStamp).TotalMilliseconds >= 2000) RefreshProcesses();
                    break;
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_ready) RefreshActiveTab(false);
        }

        private void RefreshActiveTab(bool force)
        {
            if (!_ready) return;

            switch (tabControl1.SelectedIndex)
            {
                case 0:
                    if (force || (DateTime.Now - _hardwareStamp).TotalMilliseconds >= 1900) RefreshHardware();
                    break;
                case 1:
                    if (force || !_infoLoaded || (DateTime.Now - _infoStamp).TotalSeconds >= 60) RefreshInfo(force);
                    break;
                case 2:
                    if (force || (DateTime.Now - _servicesStamp).TotalMilliseconds >= 7000) RefreshServices();
                    break;
                case 3:
                    if (force || (DateTime.Now - _processesStamp).TotalMilliseconds >= 2000) RefreshProcesses();
                    break;
            }
        }

        private async void RefreshHardware()
        {
            if (!_ready || _hardwareBusy || _provider == null) return;

            _hardwareBusy = true;
            try
            {
                IList<HwNode> tree = await Task.Run(() => BuildHardwareTree());
                if (IsDisposed) return;

                hwTree.SetRoots(tree);
                labelUpdated.Text = "updated " + DateTime.Now.ToString("HH:mm:ss");
                labelStatus.Text = _provider.Status;
                labelStatus.ForeColor = Theme.TextDim;
                labelSummary.Text = _provider.Summary;

                if (_overlay != null) _overlay.UpdateSnapshot(_provider.GetHudSnapshot());

                _hardwareStamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                ShowError("Hardware read failed: " + ex.Message);
            }
            finally
            {
                _hardwareBusy = false;
            }
        }

        private void buttonInfoRefresh_Click(object sender, EventArgs e)
        {
            RefreshInfo(true);
        }

        private void buttonInfoCopy_Click(object sender, EventArgs e)
        {
            InfoPanel panel = ActiveInfoPanel();
            if (panel != null) panel.CopySelectedOrAll();
        }

        private InfoPanel ActiveInfoPanel()
        {
            switch (tabInfo.SelectedIndex)
            {
                case 0: return infoPanelCpu;
                case 1: return infoPanelGpu;
                case 2: return infoPanelRam;
                case 3: return infoPanelMainboard;
                case 4: return infoPanelStorage;
                default: return null;
            }
        }

        private async void RefreshInfo(bool force)
        {
            if (!_ready || _infoBusy) return;
            if (!force && _infoLoaded && (DateTime.Now - _infoStamp).TotalSeconds < 60) return;

            _infoBusy = true;
            try
            {
                InfoBundle bundle = await Task.Run(() => new InfoBundle
                {
                    Cpu = HardwareDetails.GetCpuSections(),
                    Gpu = HardwareDetails.GetGpuSections(),
                    Ram = HardwareDetails.GetMemorySections(),
                    Board = HardwareDetails.GetMainboardSections(),
                    Storage = HardwareDetails.GetStorageSections()
                });
                if (IsDisposed) return;

                infoPanelCpu.SetSections(bundle.Cpu);
                infoPanelGpu.SetSections(bundle.Gpu);
                infoPanelRam.SetSections(bundle.Ram);
                infoPanelMainboard.SetSections(bundle.Board);
                infoPanelStorage.SetSections(bundle.Storage);

                labelInfoUpdated.Text = "updated " + DateTime.Now.ToString("HH:mm:ss");
                _infoLoaded = true;
                _infoStamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                ShowError("Hardware details read failed: " + ex.Message);
            }
            finally
            {
                _infoBusy = false;
            }
        }

        private sealed class InfoBundle
        {
            public IList<InfoSection> Cpu;
            public IList<InfoSection> Gpu;
            public IList<InfoSection> Ram;
            public IList<InfoSection> Board;
            public IList<InfoSection> Storage;
        }

        private IList<HwNode> BuildHardwareTree()
        {
            var groups = new List<HwNode>();
            AddGroup(groups, "SYSTEM", ReadGroup(_provider.GetSystemNodes), SystemColumns);
            AddGroup(groups, "CPU", ReadGroup(_provider.GetCpuNodes), CpuColumns);
            AddGroup(groups, "MEMORY", ReadGroup(_provider.GetMemoryNodes), MemoryColumns);
            AddGroup(groups, "GRAPHICS", ReadGroup(_provider.GetGpuNodes), GpuColumns);
            AddGroup(groups, "STORAGE", ReadGroup(_provider.GetStorageNodes), StorageColumns);
            AddGroup(groups, "MAINBOARD", ReadGroup(_provider.GetMainboardNodes), BoardColumns);
            return groups;
        }

        private static void AddGroup(List<HwNode> groups, string title, IList<HwNode> children,
                                     IEnumerable<HwColumnDef> columns)
        {
            var group = new HwNode(title, HwNodeKind.Group)
            {
                Expanded = true,
                Columns = new List<HwColumnDef>(columns)
            };
            group.AddRange(children);
            groups.Add(group);
        }

        private static IList<HwNode> ReadGroup(Func<IList<HwNode>> read)
        {
            try
            {
                return read() ?? new List<HwNode>();
            }
            catch (Exception ex)
            {
                var node = new HwNode("Unavailable", HwNodeKind.Component);
                node.Set(HwCol.Info, ex.Message, HwLevel.Critical);
                return new List<HwNode> { node };
            }
        }

        private async void RefreshServices()
        {
            if (!_ready || _servicesBusy) return;

            _servicesBusy = true;
            try
            {
                List<ServiceEntry> services = await Task.Run(() => SystemLists.GetServices());
                if (IsDisposed) return;

                _services = services;
                ApplyServicesFilter();
                _servicesStamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                ShowError("Services read failed: " + ex.Message);
            }
            finally
            {
                _servicesBusy = false;
            }
        }

        private async void RefreshProcesses()
        {
            if (!_ready || _processesBusy) return;

            _processesBusy = true;
            try
            {
                List<ProcessEntry> processes = await Task.Run(() => _cpuTracker.Sample());
                if (IsDisposed) return;

                _processes = processes;
                ApplyProcessesFilter();
                _processesStamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                ShowError("Processes read failed: " + ex.Message);
            }
            finally
            {
                _processesBusy = false;
            }
        }

        private void ShowError(string message)
        {
            if (IsDisposed || !IsHandleCreated) return;
            labelStatus.Text = message;
            labelStatus.ForeColor = Theme.Danger;
        }

        // ==================================================================
        // Службы
        // ==================================================================

        private void ApplyServicesFilter()
        {
            if (!_ready) return;

            string filter = textBoxSearch.Text.Trim();
            bool runningOnly = checkRunningOnly.Checked;

            var items = new List<ServiceEntry>();
            int running = 0;
            foreach (ServiceEntry service in _services)
            {
                if (service.IsRunning) running++;
                if (runningOnly && !service.IsRunning) continue;
                if (filter.Length > 0 &&
                    !Contains(service.DisplayName, filter) &&
                    !Contains(service.Name, filter) &&
                    !Contains(service.Status, filter) &&
                    !Contains(service.StartMode, filter)) continue;
                items.Add(service);
            }

            SortServices(items);

            var rows = new List<GridRow>(items.Count);
            foreach (ServiceEntry service in items)
            {
                var row = new GridRow(new[] { service.DisplayName, service.Name, service.Status, service.StartMode },
                    service.Name);
                row.SetLevel(2, StatusLevel(service.Status));
                rows.Add(row);
            }

            dataGridServices.SetRows(rows);
            dataGridServices.SortColumn = _serviceSort;
            dataGridServices.SortAscending = _serviceAscending;
            labelServicesSummary.Text = string.Format("{0} services  \u00B7  {1} running", _services.Count, running);
        }

        private void SortServices(List<ServiceEntry> items)
        {
            if (_serviceSort < 0) return;

            Comparison<ServiceEntry> comparison;
            switch (_serviceSort)
            {
                case 0: comparison = (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase); break;
                case 1: comparison = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                case 2: comparison = (a, b) => string.Compare(a.Status, b.Status, StringComparison.OrdinalIgnoreCase); break;
                case 3: comparison = (a, b) => string.Compare(a.StartMode, b.StartMode, StringComparison.OrdinalIgnoreCase); break;
                default: return;
            }

            items.Sort(comparison);
            if (!_serviceAscending) items.Reverse();
        }

        private void dataGridServices_ColumnHeaderClicked(object sender, ColumnClickEventArgs e)
        {
            if (_serviceSort == e.Column) _serviceAscending = !_serviceAscending;
            else
            {
                _serviceSort = e.Column;
                _serviceAscending = true;
            }
            ApplyServicesFilter();
        }

        private void checkRunningOnly_CheckedChanged(object sender, EventArgs e)
        {
            ApplyServicesFilter();
        }

        private static HwLevel StatusLevel(string status)
        {
            if (string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase)) return HwLevel.Accent;
            if (string.Equals(status, "Stopped", StringComparison.OrdinalIgnoreCase)) return HwLevel.Dim;
            return HwLevel.Warn;
        }

        // ==================================================================
        // Процессы
        // ==================================================================

        private void ApplyProcessesFilter()
        {
            if (!_ready) return;

            string filter = textBoxSearch.Text.Trim();
            var items = new List<ProcessEntry>();
            double totalCpu = 0;

            foreach (ProcessEntry process in _processes)
            {
                totalCpu += process.CpuPercent;
                if (filter.Length > 0 &&
                    !Contains(process.Name, filter) &&
                    !process.Pid.ToString().Contains(filter)) continue;
                items.Add(process);
            }

            SortProcesses(items);

            var rows = new List<GridRow>(items.Count);
            foreach (ProcessEntry process in items)
            {
                var row = new GridRow(new[]
                {
                    process.Name,
                    process.Pid.ToString(),
                    HwFormat.Percent(process.CpuPercent),
                    HwFormat.Bytes(process.WorkingSet),
                    process.Threads.ToString(),
                    process.Handles.ToString(),
                    process.Started
                }, process.Pid);

                if (process.CpuPercent >= 50) row.SetLevel(2, HwLevel.Warn);
                rows.Add(row);
            }

            dataGridProcesses.SetRows(rows);
            dataGridProcesses.SortColumn = _processSort;
            dataGridProcesses.SortAscending = _processAscending;
            labelProcessesSummary.Text = string.Format("{0} processes  \u00B7  CPU {1}",
                _processes.Count, HwFormat.Percent(totalCpu));
        }

        private void SortProcesses(List<ProcessEntry> items)
        {
            Comparison<ProcessEntry> comparison;
            switch (_processSort)
            {
                case 0: comparison = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                case 1: comparison = (a, b) => a.Pid.CompareTo(b.Pid); break;
                case 2: comparison = (a, b) => a.CpuPercent.CompareTo(b.CpuPercent); break;
                case 3: comparison = (a, b) => a.WorkingSet.CompareTo(b.WorkingSet); break;
                case 4: comparison = (a, b) => a.Threads.CompareTo(b.Threads); break;
                case 5: comparison = (a, b) => a.Handles.CompareTo(b.Handles); break;
                default: comparison = (a, b) => string.Compare(a.Started, b.Started, StringComparison.Ordinal); break;
            }

            items.Sort(comparison);
            if (!_processAscending) items.Reverse();
        }

        private void dataGridProcesses_ColumnHeaderClicked(object sender, ColumnClickEventArgs e)
        {
            if (_processSort == e.Column) _processAscending = !_processAscending;
            else
            {
                _processSort = e.Column;
                // Для чисел логичнее начинать с убывания (самые большие сверху).
                _processAscending = e.Column == 0;
            }
            ApplyProcessesFilter();
        }

        // ==================================================================
        // Поиск и кнопки
        // ==================================================================

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            ApplySearch();
        }

        private void textBoxSearch_Enter(object sender, EventArgs e)
        {
            labelSearchHint.Visible = false;
            panelSearch.Invalidate();
        }

        private void textBoxSearch_Leave(object sender, EventArgs e)
        {
            labelSearchHint.Visible = textBoxSearch.Text.Length == 0;
            panelSearch.Invalidate();
        }

        private void textBoxSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                textBoxSearch.Clear();
                e.Handled = true;
            }
        }

        private void labelSearchHint_Click(object sender, EventArgs e)
        {
            textBoxSearch.Focus();
        }

        private void ApplySearch()
        {
            string text = textBoxSearch.Text.Trim();
            labelSearchHint.Visible = !textBoxSearch.Focused && text.Length == 0;

            if (!_ready) return;

            hwTree.FilterText = text;
            ApplyServicesFilter();
            ApplyProcessesFilter();
        }

        private void buttonExpandAll_Click(object sender, EventArgs e)
        {
            hwTree.SetAllExpanded(true);
        }

        private void buttonCollapseAll_Click(object sender, EventArgs e)
        {
            hwTree.SetAllExpanded(false);
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            RefreshActiveTab(true);
        }

        private void buttonOverlay_Click(object sender, EventArgs e)
        {
            if (_overlay == null)
            {
                var hud = new HudForm();
                _overlay = hud;

                hud.FormClosed += delegate
                {
                    if (_overlay == hud) _overlay = null;
                    if (!IsDisposed)
                    {
                        buttonOverlay.Text = "Overlay: off";
                        buttonOverlay.ForeColor = Theme.Text;
                    }
                };

                hud.Show();
                buttonOverlay.Text = "Overlay: on";
                buttonOverlay.ForeColor = Theme.Accent;

                if (_provider != null) hud.UpdateSnapshot(_provider.GetHudSnapshot());
            }
            else
            {
                _overlay.Close();
            }
        }

        private static bool Contains(string text, string filter)
        {
            return text != null && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
