namespace Berlit
{
    partial class Berlit
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора - не изменяйте
        /// содержимое этого метода с помощью редактора кода.
        /// Здесь только раскладка и события; цвета, шрифты и стили задаются
        /// в ApplyTheme() (Berlit.cs), чтобы тема была в одном месте.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelSearch = new System.Windows.Forms.Panel();
            this.labelSearchHint = new System.Windows.Forms.Label();
            this.textBoxSearch = new System.Windows.Forms.TextBox();
            this.buttonOverlay = new System.Windows.Forms.Button();
            this.labelSubtitle = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.tabControl1 = new DarkTabControl();
            this.tabPageMonitoringPC = new System.Windows.Forms.TabPage();
            this.hwTree = new HwTreeGrid();
            this.panelPcToolbar = new System.Windows.Forms.Panel();
            this.labelUpdated = new System.Windows.Forms.Label();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonCollapseAll = new System.Windows.Forms.Button();
            this.buttonExpandAll = new System.Windows.Forms.Button();
            this.tabPageMonitoringServices = new System.Windows.Forms.TabPage();
            this.dataGridServices = new DataTableGrid();
            this.panelServicesToolbar = new System.Windows.Forms.Panel();
            this.checkRunningOnly = new DarkCheckBox();
            this.labelServicesSummary = new System.Windows.Forms.Label();
            this.tabPageSystemInfo = new System.Windows.Forms.TabPage();
            this.panelInfoToolbar = new System.Windows.Forms.Panel();
            this.labelInfoSummary = new System.Windows.Forms.Label();
            this.labelInfoUpdated = new System.Windows.Forms.Label();
            this.buttonInfoRefresh = new System.Windows.Forms.Button();
            this.buttonInfoCopy = new System.Windows.Forms.Button();
            this.tabInfo = new DarkTabControl();
            this.tabInfoCpu = new System.Windows.Forms.TabPage();
            this.tabInfoGpu = new System.Windows.Forms.TabPage();
            this.tabInfoRam = new System.Windows.Forms.TabPage();
            this.tabInfoMainboard = new System.Windows.Forms.TabPage();
            this.tabInfoStorage = new System.Windows.Forms.TabPage();
            this.infoPanelCpu = new InfoPanel();
            this.infoPanelGpu = new InfoPanel();
            this.infoPanelRam = new InfoPanel();
            this.infoPanelMainboard = new InfoPanel();
            this.infoPanelStorage = new InfoPanel();
            this.tabPageProcesses = new System.Windows.Forms.TabPage();
            this.dataGridProcesses = new DataTableGrid();
            this.panelProcessesToolbar = new System.Windows.Forms.Panel();
            this.labelProcessesSummary = new System.Windows.Forms.Label();
            this.panelStatus = new System.Windows.Forms.Panel();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelSummary = new System.Windows.Forms.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.panelTop.SuspendLayout();
            this.panelSearch.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPageMonitoringPC.SuspendLayout();
            this.panelPcToolbar.SuspendLayout();
            this.tabPageSystemInfo.SuspendLayout();
            this.panelInfoToolbar.SuspendLayout();
            this.tabInfo.SuspendLayout();
            this.tabInfoCpu.SuspendLayout();
            this.tabInfoGpu.SuspendLayout();
            this.tabInfoRam.SuspendLayout();
            this.tabInfoMainboard.SuspendLayout();
            this.tabInfoStorage.SuspendLayout();
            this.tabPageMonitoringServices.SuspendLayout();
            this.panelServicesToolbar.SuspendLayout();
            this.tabPageProcesses.SuspendLayout();
            this.panelProcessesToolbar.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.panelSearch);
            this.panelTop.Controls.Add(this.buttonOverlay);
            this.panelTop.Controls.Add(this.labelSubtitle);
            this.panelTop.Controls.Add(this.labelTitle);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1240, 52);
            this.panelTop.TabIndex = 1;
            // 
            // buttonOverlay
            // 
            this.buttonOverlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOverlay.Location = new System.Drawing.Point(802, 11);
            this.buttonOverlay.Name = "buttonOverlay";
            this.buttonOverlay.Size = new System.Drawing.Size(112, 30);
            this.buttonOverlay.TabIndex = 3;
            this.buttonOverlay.Text = "Overlay: off";
            this.buttonOverlay.UseVisualStyleBackColor = false;
            this.buttonOverlay.Click += new System.EventHandler(this.buttonOverlay_Click);
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Location = new System.Drawing.Point(26, 9);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(52, 20);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "BERLIT";
            // 
            // labelSubtitle
            // 
            this.labelSubtitle.AutoSize = true;
            this.labelSubtitle.Location = new System.Drawing.Point(28, 31);
            this.labelSubtitle.Name = "labelSubtitle";
            this.labelSubtitle.Size = new System.Drawing.Size(120, 13);
            this.labelSubtitle.TabIndex = 1;
            this.labelSubtitle.Text = "HARDWARE MONITOR";
            // 
            // panelSearch
            // 
            this.panelSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.panelSearch.Controls.Add(this.labelSearchHint);
            this.panelSearch.Controls.Add(this.textBoxSearch);
            this.panelSearch.Location = new System.Drawing.Point(926, 11);
            this.panelSearch.Name = "panelSearch";
            this.panelSearch.Size = new System.Drawing.Size(300, 30);
            this.panelSearch.TabIndex = 2;
            this.panelSearch.Paint += new System.Windows.Forms.PaintEventHandler(this.panelSearch_Paint);
            // 
            // textBoxSearch
            // 
            this.textBoxSearch.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxSearch.Location = new System.Drawing.Point(30, 7);
            this.textBoxSearch.Name = "textBoxSearch";
            this.textBoxSearch.Size = new System.Drawing.Size(258, 15);
            this.textBoxSearch.TabIndex = 1;
            this.textBoxSearch.TextChanged += new System.EventHandler(this.textBoxSearch_TextChanged);
            this.textBoxSearch.Enter += new System.EventHandler(this.textBoxSearch_Enter);
            this.textBoxSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxSearch_KeyDown);
            this.textBoxSearch.Leave += new System.EventHandler(this.textBoxSearch_Leave);
            // 
            // labelSearchHint
            // 
            this.labelSearchHint.AutoSize = false;
            this.labelSearchHint.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(44)))), ((int)(((byte)(44)))));
            this.labelSearchHint.Location = new System.Drawing.Point(30, 7);
            this.labelSearchHint.Name = "labelSearchHint";
            this.labelSearchHint.Size = new System.Drawing.Size(258, 16);
            this.labelSearchHint.TabIndex = 0;
            this.labelSearchHint.Text = "Search sensors, services, processes";
            this.labelSearchHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelSearchHint.Click += new System.EventHandler(this.labelSearchHint_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageMonitoringPC);
            this.tabControl1.Controls.Add(this.tabPageSystemInfo);
            this.tabControl1.Controls.Add(this.tabPageMonitoringServices);
            this.tabControl1.Controls.Add(this.tabPageProcesses);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 52);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1240, 720);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabPageMonitoringPC
            // 
            this.tabPageMonitoringPC.Controls.Add(this.hwTree);
            this.tabPageMonitoringPC.Controls.Add(this.panelPcToolbar);
            this.tabPageMonitoringPC.Location = new System.Drawing.Point(4, 38);
            this.tabPageMonitoringPC.Name = "tabPageMonitoringPC";
            this.tabPageMonitoringPC.Padding = new System.Windows.Forms.Padding(1);
            this.tabPageMonitoringPC.Size = new System.Drawing.Size(1232, 678);
            this.tabPageMonitoringPC.TabIndex = 0;
            this.tabPageMonitoringPC.Text = "PC INFO";
            this.tabPageMonitoringPC.UseVisualStyleBackColor = false;
            // 
            // panelPcToolbar
            // 
            this.panelPcToolbar.Controls.Add(this.labelUpdated);
            this.panelPcToolbar.Controls.Add(this.buttonRefresh);
            this.panelPcToolbar.Controls.Add(this.buttonCollapseAll);
            this.panelPcToolbar.Controls.Add(this.buttonExpandAll);
            this.panelPcToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPcToolbar.Location = new System.Drawing.Point(1, 1);
            this.panelPcToolbar.Name = "panelPcToolbar";
            this.panelPcToolbar.Size = new System.Drawing.Size(1230, 38);
            this.panelPcToolbar.TabIndex = 0;
            // 
            // buttonExpandAll
            // 
            this.buttonExpandAll.Location = new System.Drawing.Point(12, 7);
            this.buttonExpandAll.Name = "buttonExpandAll";
            this.buttonExpandAll.Size = new System.Drawing.Size(104, 24);
            this.buttonExpandAll.TabIndex = 0;
            this.buttonExpandAll.Text = "Expand all";
            this.buttonExpandAll.UseVisualStyleBackColor = false;
            this.buttonExpandAll.Click += new System.EventHandler(this.buttonExpandAll_Click);
            // 
            // buttonCollapseAll
            // 
            this.buttonCollapseAll.Location = new System.Drawing.Point(122, 7);
            this.buttonCollapseAll.Name = "buttonCollapseAll";
            this.buttonCollapseAll.Size = new System.Drawing.Size(104, 24);
            this.buttonCollapseAll.TabIndex = 1;
            this.buttonCollapseAll.Text = "Collapse all";
            this.buttonCollapseAll.UseVisualStyleBackColor = false;
            this.buttonCollapseAll.Click += new System.EventHandler(this.buttonCollapseAll_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(232, 7);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(96, 24);
            this.buttonRefresh.TabIndex = 2;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = false;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // labelUpdated
            // 
            this.labelUpdated.Dock = System.Windows.Forms.DockStyle.Right;
            this.labelUpdated.Location = new System.Drawing.Point(980, 0);
            this.labelUpdated.Name = "labelUpdated";
            this.labelUpdated.Padding = new System.Windows.Forms.Padding(0, 0, 14, 0);
            this.labelUpdated.Size = new System.Drawing.Size(250, 38);
            this.labelUpdated.TabIndex = 3;
            this.labelUpdated.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // hwTree
            // 
            this.hwTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hwTree.Location = new System.Drawing.Point(1, 39);
            this.hwTree.Name = "hwTree";
            this.hwTree.Size = new System.Drawing.Size(1230, 638);
            this.hwTree.TabIndex = 1;
            // 
            // tabPageSystemInfo
            // 
            this.tabPageSystemInfo.Controls.Add(this.tabInfo);
            this.tabPageSystemInfo.Controls.Add(this.panelInfoToolbar);
            this.tabPageSystemInfo.Location = new System.Drawing.Point(4, 38);
            this.tabPageSystemInfo.Name = "tabPageSystemInfo";
            this.tabPageSystemInfo.Padding = new System.Windows.Forms.Padding(1);
            this.tabPageSystemInfo.Size = new System.Drawing.Size(1232, 678);
            this.tabPageSystemInfo.TabIndex = 3;
            this.tabPageSystemInfo.Text = "SYSTEM INFO";
            this.tabPageSystemInfo.UseVisualStyleBackColor = false;
            // 
            // panelInfoToolbar
            // 
            this.panelInfoToolbar.Controls.Add(this.labelInfoUpdated);
            this.panelInfoToolbar.Controls.Add(this.labelInfoSummary);
            this.panelInfoToolbar.Controls.Add(this.buttonInfoCopy);
            this.panelInfoToolbar.Controls.Add(this.buttonInfoRefresh);
            this.panelInfoToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelInfoToolbar.Location = new System.Drawing.Point(1, 1);
            this.panelInfoToolbar.Name = "panelInfoToolbar";
            this.panelInfoToolbar.Size = new System.Drawing.Size(1230, 38);
            this.panelInfoToolbar.TabIndex = 0;
            // 
            // buttonInfoRefresh
            // 
            this.buttonInfoRefresh.Location = new System.Drawing.Point(12, 7);
            this.buttonInfoRefresh.Name = "buttonInfoRefresh";
            this.buttonInfoRefresh.Size = new System.Drawing.Size(96, 24);
            this.buttonInfoRefresh.TabIndex = 0;
            this.buttonInfoRefresh.Text = "Refresh";
            this.buttonInfoRefresh.UseVisualStyleBackColor = false;
            this.buttonInfoRefresh.Click += new System.EventHandler(this.buttonInfoRefresh_Click);
            // 
            // buttonInfoCopy
            // 
            this.buttonInfoCopy.Location = new System.Drawing.Point(116, 7);
            this.buttonInfoCopy.Name = "buttonInfoCopy";
            this.buttonInfoCopy.Size = new System.Drawing.Size(80, 24);
            this.buttonInfoCopy.TabIndex = 1;
            this.buttonInfoCopy.Text = "Copy";
            this.buttonInfoCopy.UseVisualStyleBackColor = false;
            this.buttonInfoCopy.Click += new System.EventHandler(this.buttonInfoCopy_Click);
            // 
            // labelInfoSummary
            // 
            this.labelInfoSummary.AutoSize = true;
            this.labelInfoSummary.Location = new System.Drawing.Point(212, 12);
            this.labelInfoSummary.Name = "labelInfoSummary";
            this.labelInfoSummary.Size = new System.Drawing.Size(180, 15);
            this.labelInfoSummary.TabIndex = 2;
            this.labelInfoSummary.Text = "Detailed hardware information";
            // 
            // labelInfoUpdated
            // 
            this.labelInfoUpdated.Dock = System.Windows.Forms.DockStyle.Right;
            this.labelInfoUpdated.Location = new System.Drawing.Point(980, 0);
            this.labelInfoUpdated.Name = "labelInfoUpdated";
            this.labelInfoUpdated.Padding = new System.Windows.Forms.Padding(0, 0, 14, 0);
            this.labelInfoUpdated.Size = new System.Drawing.Size(250, 38);
            this.labelInfoUpdated.TabIndex = 3;
            this.labelInfoUpdated.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tabInfo
            // 
            this.tabInfo.Compact = true;
            this.tabInfo.Controls.Add(this.tabInfoCpu);
            this.tabInfo.Controls.Add(this.tabInfoGpu);
            this.tabInfo.Controls.Add(this.tabInfoRam);
            this.tabInfo.Controls.Add(this.tabInfoMainboard);
            this.tabInfo.Controls.Add(this.tabInfoStorage);
            this.tabInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabInfo.Location = new System.Drawing.Point(1, 39);
            this.tabInfo.Name = "tabInfo";
            this.tabInfo.SelectedIndex = 0;
            this.tabInfo.Size = new System.Drawing.Size(1230, 638);
            this.tabInfo.TabIndex = 1;
            // 
            // tabInfoCpu
            // 
            this.tabInfoCpu.Controls.Add(this.infoPanelCpu);
            this.tabInfoCpu.Location = new System.Drawing.Point(4, 32);
            this.tabInfoCpu.Name = "tabInfoCpu";
            this.tabInfoCpu.Padding = new System.Windows.Forms.Padding(1);
            this.tabInfoCpu.Size = new System.Drawing.Size(1222, 602);
            this.tabInfoCpu.TabIndex = 0;
            this.tabInfoCpu.Text = "CPU";
            this.tabInfoCpu.UseVisualStyleBackColor = false;
            // 
            // infoPanelCpu
            // 
            this.infoPanelCpu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoPanelCpu.Location = new System.Drawing.Point(1, 1);
            this.infoPanelCpu.Name = "infoPanelCpu";
            this.infoPanelCpu.Size = new System.Drawing.Size(1220, 600);
            this.infoPanelCpu.TabIndex = 0;
            // 
            // tabInfoGpu
            // 
            this.tabInfoGpu.Controls.Add(this.infoPanelGpu);
            this.tabInfoGpu.Location = new System.Drawing.Point(4, 32);
            this.tabInfoGpu.Name = "tabInfoGpu";
            this.tabInfoGpu.Padding = new System.Windows.Forms.Padding(1);
            this.tabInfoGpu.Size = new System.Drawing.Size(1222, 602);
            this.tabInfoGpu.TabIndex = 1;
            this.tabInfoGpu.Text = "GPU";
            this.tabInfoGpu.UseVisualStyleBackColor = false;
            // 
            // infoPanelGpu
            // 
            this.infoPanelGpu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoPanelGpu.Location = new System.Drawing.Point(1, 1);
            this.infoPanelGpu.Name = "infoPanelGpu";
            this.infoPanelGpu.Size = new System.Drawing.Size(1220, 600);
            this.infoPanelGpu.TabIndex = 0;
            // 
            // tabInfoRam
            // 
            this.tabInfoRam.Controls.Add(this.infoPanelRam);
            this.tabInfoRam.Location = new System.Drawing.Point(4, 32);
            this.tabInfoRam.Name = "tabInfoRam";
            this.tabInfoRam.Padding = new System.Windows.Forms.Padding(1);
            this.tabInfoRam.Size = new System.Drawing.Size(1222, 602);
            this.tabInfoRam.TabIndex = 2;
            this.tabInfoRam.Text = "RAM";
            this.tabInfoRam.UseVisualStyleBackColor = false;
            // 
            // infoPanelRam
            // 
            this.infoPanelRam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoPanelRam.Location = new System.Drawing.Point(1, 1);
            this.infoPanelRam.Name = "infoPanelRam";
            this.infoPanelRam.Size = new System.Drawing.Size(1220, 600);
            this.infoPanelRam.TabIndex = 0;
            // 
            // tabInfoMainboard
            // 
            this.tabInfoMainboard.Controls.Add(this.infoPanelMainboard);
            this.tabInfoMainboard.Location = new System.Drawing.Point(4, 32);
            this.tabInfoMainboard.Name = "tabInfoMainboard";
            this.tabInfoMainboard.Padding = new System.Windows.Forms.Padding(1);
            this.tabInfoMainboard.Size = new System.Drawing.Size(1222, 602);
            this.tabInfoMainboard.TabIndex = 3;
            this.tabInfoMainboard.Text = "MAINBOARD";
            this.tabInfoMainboard.UseVisualStyleBackColor = false;
            // 
            // infoPanelMainboard
            // 
            this.infoPanelMainboard.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoPanelMainboard.Location = new System.Drawing.Point(1, 1);
            this.infoPanelMainboard.Name = "infoPanelMainboard";
            this.infoPanelMainboard.Size = new System.Drawing.Size(1220, 600);
            this.infoPanelMainboard.TabIndex = 0;
            // 
            // tabInfoStorage
            // 
            this.tabInfoStorage.Controls.Add(this.infoPanelStorage);
            this.tabInfoStorage.Location = new System.Drawing.Point(4, 32);
            this.tabInfoStorage.Name = "tabInfoStorage";
            this.tabInfoStorage.Padding = new System.Windows.Forms.Padding(1);
            this.tabInfoStorage.Size = new System.Drawing.Size(1222, 602);
            this.tabInfoStorage.TabIndex = 4;
            this.tabInfoStorage.Text = "STORAGE";
            this.tabInfoStorage.UseVisualStyleBackColor = false;
            // 
            // infoPanelStorage
            // 
            this.infoPanelStorage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoPanelStorage.Location = new System.Drawing.Point(1, 1);
            this.infoPanelStorage.Name = "infoPanelStorage";
            this.infoPanelStorage.Size = new System.Drawing.Size(1220, 600);
            this.infoPanelStorage.TabIndex = 0;
            // 
            // tabPageMonitoringServices
            // 
            this.tabPageMonitoringServices.Controls.Add(this.dataGridServices);
            this.tabPageMonitoringServices.Controls.Add(this.panelServicesToolbar);
            this.tabPageMonitoringServices.Location = new System.Drawing.Point(4, 38);
            this.tabPageMonitoringServices.Name = "tabPageMonitoringServices";
            this.tabPageMonitoringServices.Padding = new System.Windows.Forms.Padding(1);
            this.tabPageMonitoringServices.Size = new System.Drawing.Size(1232, 678);
            this.tabPageMonitoringServices.TabIndex = 1;
            this.tabPageMonitoringServices.Text = "SERVICES";
            this.tabPageMonitoringServices.UseVisualStyleBackColor = false;
            // 
            // panelServicesToolbar
            // 
            this.panelServicesToolbar.Controls.Add(this.checkRunningOnly);
            this.panelServicesToolbar.Controls.Add(this.labelServicesSummary);
            this.panelServicesToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelServicesToolbar.Location = new System.Drawing.Point(1, 1);
            this.panelServicesToolbar.Name = "panelServicesToolbar";
            this.panelServicesToolbar.Size = new System.Drawing.Size(1230, 38);
            this.panelServicesToolbar.TabIndex = 0;
            // 
            // labelServicesSummary
            // 
            this.labelServicesSummary.AutoSize = true;
            this.labelServicesSummary.Location = new System.Drawing.Point(14, 12);
            this.labelServicesSummary.Name = "labelServicesSummary";
            this.labelServicesSummary.Size = new System.Drawing.Size(100, 15);
            this.labelServicesSummary.TabIndex = 0;
            this.labelServicesSummary.Text = "Reading services...";
            // 
            // checkRunningOnly
            // 
            this.checkRunningOnly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkRunningOnly.AutoSize = false;
            this.checkRunningOnly.Location = new System.Drawing.Point(1092, 10);
            this.checkRunningOnly.Name = "checkRunningOnly";
            this.checkRunningOnly.Size = new System.Drawing.Size(126, 20);
            this.checkRunningOnly.TabIndex = 1;
            this.checkRunningOnly.Text = "Running only";
            this.checkRunningOnly.UseVisualStyleBackColor = false;
            this.checkRunningOnly.CheckedChanged += new System.EventHandler(this.checkRunningOnly_CheckedChanged);
            // 
            // dataGridServices
            // 
            this.dataGridServices.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridServices.Location = new System.Drawing.Point(1, 39);
            this.dataGridServices.Name = "dataGridServices";
            this.dataGridServices.Size = new System.Drawing.Size(1230, 638);
            this.dataGridServices.TabIndex = 1;
            // 
            // tabPageProcesses
            // 
            this.tabPageProcesses.Controls.Add(this.dataGridProcesses);
            this.tabPageProcesses.Controls.Add(this.panelProcessesToolbar);
            this.tabPageProcesses.Location = new System.Drawing.Point(4, 38);
            this.tabPageProcesses.Name = "tabPageProcesses";
            this.tabPageProcesses.Padding = new System.Windows.Forms.Padding(1);
            this.tabPageProcesses.Size = new System.Drawing.Size(1232, 678);
            this.tabPageProcesses.TabIndex = 2;
            this.tabPageProcesses.Text = "PROCESSES";
            this.tabPageProcesses.UseVisualStyleBackColor = false;
            // 
            // panelProcessesToolbar
            // 
            this.panelProcessesToolbar.Controls.Add(this.labelProcessesSummary);
            this.panelProcessesToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelProcessesToolbar.Location = new System.Drawing.Point(1, 1);
            this.panelProcessesToolbar.Name = "panelProcessesToolbar";
            this.panelProcessesToolbar.Size = new System.Drawing.Size(1230, 38);
            this.panelProcessesToolbar.TabIndex = 0;
            // 
            // labelProcessesSummary
            // 
            this.labelProcessesSummary.AutoSize = true;
            this.labelProcessesSummary.Location = new System.Drawing.Point(14, 12);
            this.labelProcessesSummary.Name = "labelProcessesSummary";
            this.labelProcessesSummary.Size = new System.Drawing.Size(108, 15);
            this.labelProcessesSummary.TabIndex = 0;
            this.labelProcessesSummary.Text = "Reading processes...";
            // 
            // dataGridProcesses
            // 
            this.dataGridProcesses.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridProcesses.Location = new System.Drawing.Point(1, 39);
            this.dataGridProcesses.Name = "dataGridProcesses";
            this.dataGridProcesses.Size = new System.Drawing.Size(1230, 638);
            this.dataGridProcesses.TabIndex = 1;
            // 
            // panelStatus
            // 
            this.panelStatus.Controls.Add(this.labelStatus);
            this.panelStatus.Controls.Add(this.labelSummary);
            this.panelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelStatus.Location = new System.Drawing.Point(0, 772);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.Size = new System.Drawing.Size(1240, 28);
            this.panelStatus.TabIndex = 2;
            // 
            // labelStatus
            // 
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelStatus.Location = new System.Drawing.Point(0, 0);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Padding = new System.Windows.Forms.Padding(14, 0, 0, 0);
            this.labelStatus.Size = new System.Drawing.Size(800, 28);
            this.labelStatus.TabIndex = 0;
            this.labelStatus.Text = "Starting...";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelSummary
            // 
            this.labelSummary.Dock = System.Windows.Forms.DockStyle.Right;
            this.labelSummary.Location = new System.Drawing.Point(800, 0);
            this.labelSummary.Name = "labelSummary";
            this.labelSummary.Padding = new System.Windows.Forms.Padding(0, 0, 14, 0);
            this.labelSummary.Size = new System.Drawing.Size(440, 28);
            this.labelSummary.TabIndex = 1;
            this.labelSummary.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // timerRefresh
            // 
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 1000;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // Berlit
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1240, 800);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panelStatus);
            this.Controls.Add(this.panelTop);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(1040, 640);
            this.Name = "Berlit";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Berlit - hardware monitor";
            this.Load += new System.EventHandler(this.Berlit_Load);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelSearch.ResumeLayout(false);
            this.panelSearch.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPageMonitoringPC.ResumeLayout(false);
            this.panelPcToolbar.ResumeLayout(false);
            this.panelPcToolbar.PerformLayout();
            this.tabPageSystemInfo.ResumeLayout(false);
            this.panelInfoToolbar.ResumeLayout(false);
            this.panelInfoToolbar.PerformLayout();
            this.tabInfo.ResumeLayout(false);
            this.tabInfoCpu.ResumeLayout(false);
            this.tabInfoGpu.ResumeLayout(false);
            this.tabInfoRam.ResumeLayout(false);
            this.tabInfoMainboard.ResumeLayout(false);
            this.tabInfoStorage.ResumeLayout(false);
            this.tabPageMonitoringServices.ResumeLayout(false);
            this.panelServicesToolbar.ResumeLayout(false);
            this.panelServicesToolbar.PerformLayout();
            this.tabPageProcesses.ResumeLayout(false);
            this.panelProcessesToolbar.ResumeLayout(false);
            this.panelProcessesToolbar.PerformLayout();
            this.panelStatus.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelSubtitle;
        private System.Windows.Forms.Panel panelSearch;
        private System.Windows.Forms.TextBox textBoxSearch;
        private System.Windows.Forms.Label labelSearchHint;
        private System.Windows.Forms.Button buttonOverlay;
        private DarkTabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageMonitoringPC;
        private HwTreeGrid hwTree;
        private System.Windows.Forms.Panel panelPcToolbar;
        private System.Windows.Forms.Label labelUpdated;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonCollapseAll;
        private System.Windows.Forms.Button buttonExpandAll;
        private System.Windows.Forms.TabPage tabPageMonitoringServices;
        private DataTableGrid dataGridServices;
        private System.Windows.Forms.Panel panelServicesToolbar;
        private DarkCheckBox checkRunningOnly;
        private System.Windows.Forms.Label labelServicesSummary;
        private System.Windows.Forms.TabPage tabPageSystemInfo;
        private System.Windows.Forms.Panel panelInfoToolbar;
        private System.Windows.Forms.Label labelInfoSummary;
        private System.Windows.Forms.Label labelInfoUpdated;
        private System.Windows.Forms.Button buttonInfoRefresh;
        private System.Windows.Forms.Button buttonInfoCopy;
        private DarkTabControl tabInfo;
        private System.Windows.Forms.TabPage tabInfoCpu;
        private System.Windows.Forms.TabPage tabInfoGpu;
        private System.Windows.Forms.TabPage tabInfoRam;
        private System.Windows.Forms.TabPage tabInfoMainboard;
        private System.Windows.Forms.TabPage tabInfoStorage;
        private InfoPanel infoPanelCpu;
        private InfoPanel infoPanelGpu;
        private InfoPanel infoPanelRam;
        private InfoPanel infoPanelMainboard;
        private InfoPanel infoPanelStorage;
        private System.Windows.Forms.TabPage tabPageProcesses;
        private DataTableGrid dataGridProcesses;
        private System.Windows.Forms.Panel panelProcessesToolbar;
        private System.Windows.Forms.Label labelProcessesSummary;
        private System.Windows.Forms.Panel panelStatus;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelSummary;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
