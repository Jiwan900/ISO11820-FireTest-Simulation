using ISO11820.Core;
using ISO11820.Global;
using ISO11820.Models;
using ISO11820.Services;
using Microsoft.Extensions.Configuration;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using Serilog;

namespace ISO11820.Forms;

/// <summary>
/// 主窗体 — 试验控制 + 实时数据显示 + 历史查询 + 设备校准
/// </summary>
public class MainForm : Form
{
    private readonly AppRuntime _ctx = AppRuntime.Instance;

    // 控制按钮
    private Button _btnNewTest, _btnStartHeat, _btnStopHeat, _btnStartRec, _btnStopRec, _btnSettings, _btnRecord, _btnExport;

    // 温度显示标签（LED风格）
    private Label _lblTF1, _lblTF2, _lblTS, _lblTC, _lblTCal;

    // 状态信息
    private Label _lblStatus, _lblTimer, _lblDrift, _lblSampleId;

    // 图表
    private PlotView _plotView;
    private PlotModel _plotModel;
    private LineSeries _seriesTF1, _seriesTF2, _seriesTS, _seriesTC;
    private readonly Queue<double> _chartTF1 = new(750), _chartTF2 = new(750),
                                  _chartTS = new(750), _chartTC = new(750);
    private int _chartTimeCounter;

    // 系统消息
    private RichTextBox _rtbLog;

    // 主TabControl
    private TabControl _tabControl;
    private TabPage _tabMain, _tabHistory, _tabCalib;

    // 历史查询控件
    private DateTimePicker _dtFrom, _dtTo;
    private TextBox _txtSearchPid;
    private ComboBox _cmbSearchOp;
    private DataGridView _dgvHistory;
    private Button _btnSearch, _btnExportExcel, _btnExportPdf;

    // 校准控件
    private Label _lblCalibTemp;
    private DataGridView _dgvCalib;
    private Button _btnRecordCalib, _btnRefreshCalib;

    // 状态更新用
    private TestStatus _currentStatus = TestStatus.Idle;
    private readonly List<string> _messageBuffer = new();

    public MainForm()
    {
        InitializeComponent();
        InitializeChart();
        SubscribeEvents();
        UpdateButtonStates();

        // 启动数据采集
        _ctx.DaqWorker.Start();

        FormClosed += (s, e) => _ctx.DaqWorker.Stop();

        AddSystemMessage("系统初始化，操作员：" + _ctx.CurrentOperatorName);
    }

    #region 初始化UI

    private void InitializeComponent()
    {
        Text = $"ISO 11820 建筑材料不燃性试验系统 — 当前用户：{_ctx.CurrentOperatorName} ({_ctx.CurrentUserType})";
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 40);

        // ===== TabControl =====
        _tabControl = new TabControl
        {
            Location = new Point(0, 0),
            Size = new Size(1265, 780),
            Font = new Font("Microsoft YaHei", 10)
        };
        Controls.Add(_tabControl);

        // === Tab 1: 试验主界面 ===
        _tabMain = new TabPage("试验控制");
        _tabMain.BackColor = Color.FromArgb(35, 35, 48);
        BuildMainTab();
        _tabControl.TabPages.Add(_tabMain);

        // === Tab 2: 记录查询 ===
        _tabHistory = new TabPage("记录查询");
        _tabHistory.BackColor = Color.FromArgb(35, 35, 48);
        BuildHistoryTab();
        _tabControl.TabPages.Add(_tabHistory);

        // === Tab 3: 设备校准 ===
        _tabCalib = new TabPage("设备校准");
        _tabCalib.BackColor = Color.FromArgb(35, 35, 48);
        BuildCalibrationTab();
        _tabControl.TabPages.Add(_tabCalib);
    }

    private void BuildMainTab()
    {
        // ---- 左侧：温度显示 ----
        var panelLeft = new Panel
        {
            Location = new Point(5, 5),
            Size = new Size(360, 730),
            BackColor = Color.FromArgb(40, 40, 55)
        };

        int ledY = 15;
        _lblTF1 = CreateLedLabel("炉温1", 0, ref ledY, Color.Cyan);
        _lblTF2 = CreateLedLabel("炉温2", 1, ref ledY, Color.LightGreen);
        _lblTS = CreateLedLabel("表面温度", 2, ref ledY, Color.Orange);
        _lblTC = CreateLedLabel("中心温度", 3, ref ledY, Color.Yellow);
        _lblTCal = CreateLedLabel("校准温度", 16, ref ledY, Color.Magenta);

        ledY += 15;

        // 样品编号
        _lblSampleId = new Label
        {
            Text = "样品：未创建试验",
            Font = new Font("Consolas", 11, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, ledY),
            Size = new Size(340, 28),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panelLeft.Controls.Add(_lblSampleId);
        ledY += 35;

        // 状态
        _lblStatus = new Label
        {
            Text = "● 空闲",
            Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
            ForeColor = Color.Gray,
            Location = new Point(10, ledY),
            Size = new Size(340, 35),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panelLeft.Controls.Add(_lblStatus);
        ledY += 42;

        // 计时器
        _lblTimer = new Label
        {
            Text = "⏱ 00:00:00",
            Font = new Font("Consolas", 18, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, ledY),
            Size = new Size(340, 35),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panelLeft.Controls.Add(_lblTimer);
        ledY += 40;

        // 温漂
        _lblDrift = new Label
        {
            Text = "温漂：-- ℃/10min",
            Font = new Font("Microsoft YaHei", 10),
            ForeColor = Color.LightGray,
            Location = new Point(10, ledY),
            Size = new Size(340, 22),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panelLeft.Controls.Add(_lblDrift);
        ledY += 35;

        // ---- 按钮组 ----
        var groupBtns = new GroupBox
        {
            Text = "试验控制",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, ledY),
            Size = new Size(335, 280)
        };

        int bx = 15, by = 30, bw = 140, bh = 42, gap = 10;

        _btnNewTest = MakeBtn("新建试验", bx, by, bw, bh, Color.FromArgb(0, 150, 100), OnNewTest);
        _btnStartHeat = MakeBtn("开始升温", bx + bw + gap, by, bw, bh, Color.FromArgb(200, 100, 0), OnStartHeat);
        _btnStopHeat = MakeBtn("停止升温", bx, by + bh + gap, bw, bh, Color.FromArgb(180, 60, 60), OnStopHeat);
        _btnStartRec = MakeBtn("开始记录", bx + bw + gap, by + bh + gap, bw, bh, Color.FromArgb(0, 120, 200), OnStartRecord);
        _btnStopRec = MakeBtn("停止记录", bx, by + (bh + gap) * 2, bw, bh, Color.FromArgb(180, 60, 60), OnStopRecord);
        _btnRecord = MakeBtn("试验记录", bx + bw + gap, by + (bh + gap) * 2, bw, bh, Color.FromArgb(100, 80, 160), OnOpenRecord);
        _btnExport = MakeBtn("导出报告", bx, by + (bh + gap) * 3, bw, bh, Color.FromArgb(60, 60, 160), OnExport);
        _btnSettings = MakeBtn("参数设置", bx + bw + gap, by + (bh + gap) * 3, bw, bh, Color.FromArgb(80, 80, 100), OnSettings);

        groupBtns.Controls.AddRange([_btnNewTest, _btnStartHeat, _btnStopHeat, _btnStartRec, _btnStopRec, _btnRecord, _btnExport, _btnSettings]);
        panelLeft.Controls.Add(groupBtns);

        foreach (var lbl in new[] { _lblTF1, _lblTF2, _lblTS, _lblTC, _lblTCal })
            panelLeft.Controls.Add(lbl);

        _tabMain.Controls.Add(panelLeft);

        // ---- 右侧：曲线图 ----
        _plotView = new PlotView
        {
            Location = new Point(375, 5),
            Size = new Size(875, 420),
            BackColor = Color.FromArgb(25, 25, 35)
        };
        _tabMain.Controls.Add(_plotView);

        // ---- 底部：系统消息 ----
        var panelLog = new Panel
        {
            Location = new Point(375, 435),
            Size = new Size(875, 300),
            BackColor = Color.FromArgb(25, 25, 35)
        };
        var lblLog = new Label
        {
            Text = "系统消息",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(5, 2),
            AutoSize = true
        };
        _rtbLog = new RichTextBox
        {
            Location = new Point(5, 25),
            Size = new Size(865, 270),
            BackColor = Color.FromArgb(20, 20, 30),
            ForeColor = Color.White,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        panelLog.Controls.AddRange([lblLog, _rtbLog]);
        _tabMain.Controls.Add(panelLog);
    }

    private Label CreateLedLabel(string name, int sensorId, ref int y, Color color)
    {
        var group = new GroupBox
        {
            Text = name,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(10, y),
            Size = new Size(335, 52)
        };

        var lbl = new Label
        {
            Text = "--.- ℃",
            Font = new Font("Consolas", 26, FontStyle.Bold),
            ForeColor = color,
            BackColor = Color.FromArgb(15, 15, 25),
            Location = new Point(5, 12),
            Size = new Size(320, 36),
            TextAlign = ContentAlignment.MiddleRight,
            Tag = sensorId
        };

        group.Controls.Add(lbl);
        _tabMain.Controls.Add(group);

        y += 58;
        return lbl;
    }

    private Button MakeBtn(string text, int x, int y, int w, int h, Color bg, EventHandler handler)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += handler;
        return btn;
    }

    #endregion

    #region 图表初始化

    private void InitializeChart()
    {
        _plotModel = new PlotModel { Title = "温度变化曲线", TitleColor = OxyColor.FromRgb(200, 200, 200),
            TextColor = OxyColor.FromRgb(180, 180, 180), PlotAreaBorderColor = OxyColor.FromRgb(80, 80, 100) };
        _plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "时间 (秒)", TitleColor = OxyColor.FromRgb(180, 180, 180),
            TextColor = OxyColor.FromRgb(160, 160, 160), Minimum = 0, Maximum = 600
        });
        _plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Left, Title = "温度 (℃)", TitleColor = OxyColor.FromRgb(180, 180, 180),
            TextColor = OxyColor.FromRgb(160, 160, 160), Minimum = 0, Maximum = 800
        });

        _seriesTF1 = new LineSeries { Title = "炉温1", Color = OxyColor.FromRgb(0, 255, 255), StrokeThickness = 1.5 };
        _seriesTF2 = new LineSeries { Title = "炉温2", Color = OxyColor.FromRgb(100, 255, 100), StrokeThickness = 1.5 };
        _seriesTS = new LineSeries { Title = "表面温度", Color = OxyColor.FromRgb(255, 180, 0), StrokeThickness = 1.5 };
        _seriesTC = new LineSeries { Title = "中心温度", Color = OxyColor.FromRgb(255, 255, 0), StrokeThickness = 1.5 };

        _plotModel.Series.Add(_seriesTF1);
        _plotModel.Series.Add(_seriesTF2);
        _plotModel.Series.Add(_seriesTS);
        _plotModel.Series.Add(_seriesTC);
        _plotView.Model = _plotModel;
    }

    #endregion

    #region 历史查询Tab

    private void BuildHistoryTab()
    {
        int y = 10;
        var lbl = new Label { Text = "查询条件", Font = new Font("Microsoft YaHei", 11, FontStyle.Bold), ForeColor = Color.White, Location = new Point(10, y), AutoSize = true };
        _tabHistory.Controls.Add(lbl);
        y += 30;

        AddHistLabel("日期从：", ref y);
        _dtFrom = new DateTimePicker { Location = new Point(90, y), Size = new Size(140, 25), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-3) };
        _tabHistory.Controls.Add(_dtFrom);

        AddHistLabel("到：", ref y, 240);
        _dtTo = new DateTimePicker { Location = new Point(270, y), Size = new Size(140, 25), Format = DateTimePickerFormat.Short, Value = DateTime.Now };
        _tabHistory.Controls.Add(_dtTo);
        y += 30;

        AddHistLabel("样品编号：", ref y);
        _txtSearchPid = new TextBox { Location = new Point(90, y), Size = new Size(140, 25) };
        _tabHistory.Controls.Add(_txtSearchPid);

        AddHistLabel("操作员：", ref y, 240);
        _cmbSearchOp = new ComboBox { Location = new Point(310, y), Size = new Size(120, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSearchOp.Items.Add("");
        foreach (var op in _ctx.Db.GetOperatorNames()) _cmbSearchOp.Items.Add(op);
        _cmbSearchOp.SelectedIndex = 0;
        _tabHistory.Controls.Add(_cmbSearchOp);
        y += 30;

        _btnSearch = new Button { Text = "查询", Font = new Font("Microsoft YaHei", 10, FontStyle.Bold), Location = new Point(90, y), Size = new Size(100, 35),
            BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnSearch.FlatAppearance.BorderSize = 0;
        _btnSearch.Click += OnSearchHistory;
        _tabHistory.Controls.Add(_btnSearch);

        _btnExportExcel = new Button { Text = "导出Excel", Location = new Point(200, y), Size = new Size(100, 35), BackColor = Color.FromArgb(0, 140, 80),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnExportExcel.FlatAppearance.BorderSize = 0;
        _btnExportExcel.Click += OnExportHistoryExcel;
        _tabHistory.Controls.Add(_btnExportExcel);

        _btnExportPdf = new Button { Text = "导出PDF", Location = new Point(310, y), Size = new Size(100, 35), BackColor = Color.FromArgb(140, 60, 0),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnExportPdf.FlatAppearance.BorderSize = 0;
        _btnExportPdf.Click += OnExportHistoryPdf;
        _tabHistory.Controls.Add(_btnExportPdf);

        y += 42;

        _dgvHistory = new DataGridView
        {
            Location = new Point(10, y),
            Size = new Size(1230, 620),
            BackgroundColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 80),
            Font = new Font("Microsoft YaHei", 9),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _dgvHistory.CellDoubleClick += OnHistoryDoubleClick;
        _tabHistory.Controls.Add(_dgvHistory);
    }

    private void AddHistLabel(string text, ref int y, int x = 10)
    {
        var lbl = new Label { Text = text, ForeColor = Color.White, Location = new Point(x, y + 3), AutoSize = true };
        _tabHistory.Controls.Add(lbl);
    }

    #endregion

    #region 设备校准Tab

    private void BuildCalibrationTab()
    {
        int y = 10;
        var title = new Label { Text = "设备校准", Font = new Font("Microsoft YaHei", 14, FontStyle.Bold), ForeColor = Color.White, Location = new Point(15, y), AutoSize = true };
        _tabCalib.Controls.Add(title);
        y += 40;

        var lblCal = new Label { Text = "校准温度通道当前值：", Font = new Font("Microsoft YaHei", 11), ForeColor = Color.LightGray, Location = new Point(15, y), AutoSize = true };
        _tabCalib.Controls.Add(lblCal);

        _lblCalibTemp = new Label
        {
            Text = "--.- ℃",
            Font = new Font("Consolas", 32, FontStyle.Bold),
            ForeColor = Color.Cyan,
            BackColor = Color.FromArgb(15, 15, 30),
            Location = new Point(250, y),
            Size = new Size(250, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _tabCalib.Controls.Add(_lblCalibTemp);
        y += 65;

        _btnRecordCalib = new Button { Text = "记录校准数据", Font = new Font("Microsoft YaHei", 10, FontStyle.Bold), Location = new Point(15, y), Size = new Size(140, 38),
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnRecordCalib.FlatAppearance.BorderSize = 0;
        _btnRecordCalib.Click += OnRecordCalibration;
        _tabCalib.Controls.Add(_btnRecordCalib);

        _btnRefreshCalib = new Button { Text = "刷新列表", Location = new Point(165, y), Size = new Size(100, 38), BackColor = Color.FromArgb(80, 80, 100),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnRefreshCalib.FlatAppearance.BorderSize = 0;
        _btnRefreshCalib.Click += (s, e) => LoadCalibrationHistory();
        _tabCalib.Controls.Add(_btnRefreshCalib);
        y += 48;

        _dgvCalib = new DataGridView
        {
            Location = new Point(15, y),
            Size = new Size(1220, 580),
            BackgroundColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 80),
            Font = new Font("Microsoft YaHei", 9),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _tabCalib.Controls.Add(_dgvCalib);
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        _ctx.DaqWorker.DataBroadcast += OnDataBroadcast;
        _ctx.Controller.StatusChanged += OnStatusChanged;
    }

    private void OnDataBroadcast(object? sender, DataBroadcastEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnDataBroadcast(sender, e));
            return;
        }

        // 更新温度显示
        UpdateTempLabel(_lblTF1, e.SensorValues.GetValueOrDefault(0));
        UpdateTempLabel(_lblTF2, e.SensorValues.GetValueOrDefault(1));
        UpdateTempLabel(_lblTS, e.SensorValues.GetValueOrDefault(2));
        UpdateTempLabel(_lblTC, e.SensorValues.GetValueOrDefault(3));
        UpdateTempLabel(_lblTCal, e.SensorValues.GetValueOrDefault(16));

        // 更新校准温度
        if (e.SensorValues.TryGetValue(16, out var calData))
            _lblCalibTemp.Text = $"{calData.Value:F1} ℃";

        // 更新计时器
        int secs = e.ElapsedSeconds;
        _lblTimer.Text = $"⏱ {secs / 3600:D2}:{secs / 60 % 60:D2}:{secs % 60:D2}";

        // 更新温漂
        _lblDrift.Text = $"温漂：{e.TemperatureDrift:F2} ℃/10min";

        // 更新当前状态
        _currentStatus = e.CurrentStatus;
        UpdateStatusLabel();

        // 更新图表
        UpdateChart(e);

        // 更新按钮
        UpdateButtonStates();
    }

    private void OnStatusChanged(object? sender, TestStatus newStatus)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnStatusChanged(sender, newStatus));
            return;
        }

        _currentStatus = newStatus;
        UpdateStatusLabel();
        UpdateButtonStates();

        // Complete 时同步 TotalTestTime
        if (newStatus == TestStatus.Complete && _ctx.CurrentTest != null)
        {
            _ctx.CurrentTest.TotalTestTime = _ctx.Controller.ElapsedSeconds;
        }

        // 状态切换消息
        string msg = newStatus switch
        {
            TestStatus.Preparing => "开始升温，系统升温中",
            TestStatus.Ready => "温度已稳定，可以开始记录",
            TestStatus.Recording => "开始记录，计时开始",
            TestStatus.Complete => "试验完成",
            _ => ""
        };

        if (!string.IsNullOrEmpty(msg))
            AddSystemMessage(msg);
    }

    #endregion

    #region 温度更新

    private void UpdateTempLabel(Label lbl, SensorData? data)
    {
        if (data != null)
            lbl.Text = $"{data.Value:F1} ℃";
    }

    private void UpdateStatusLabel()
    {
        (_lblStatus.Text, _lblStatus.ForeColor) = _currentStatus switch
        {
            TestStatus.Idle => ("● 空闲", Color.Gray),
            TestStatus.Preparing => ("▲ 升温中", Color.Orange),
            TestStatus.Ready => ("◆ 就绪 — 可以开始记录", Color.LimeGreen),
            TestStatus.Recording => ("■ 记录中", Color.Red),
            TestStatus.Complete => ("✓ 完成", Color.Cyan),
            _ => ("？未知", Color.White)
        };
    }

    private void UpdateChart(DataBroadcastEventArgs e)
    {
        _chartTimeCounter++;

        AddChartPoint(_seriesTF1, _chartTF1, e.SensorValues.GetValueOrDefault(0)?.Value ?? 0);
        AddChartPoint(_seriesTF2, _chartTF2, e.SensorValues.GetValueOrDefault(1)?.Value ?? 0);
        AddChartPoint(_seriesTS, _chartTS, e.SensorValues.GetValueOrDefault(2)?.Value ?? 0);
        AddChartPoint(_seriesTC, _chartTC, e.SensorValues.GetValueOrDefault(3)?.Value ?? 0);

        // 每50个点刷新一次图表
        if (_chartTimeCounter % 50 == 0)
        {
            _plotModel.InvalidatePlot(true);
        }
    }

    private void AddChartPoint(LineSeries series, Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > 750) queue.Dequeue();

        series.Points.Clear();
        var arr = queue.ToArray();
        for (int i = 0; i < arr.Length; i++)
            series.Points.Add(new DataPoint(i * 0.8, arr[i]));
    }

    #endregion

    #region 系统消息

    private void AddSystemMessage(string msg, Color? color = null)
    {
        if (InvokeRequired)
        {
            Invoke(() => AddSystemMessage(msg, color));
            return;
        }

        string time = DateTime.Now.ToString("HH:mm:ss");
        Color c = color ?? (msg.Contains("终止") ? Color.Yellow : Color.White);

        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;
        _rtbLog.SelectionColor = c;
        _rtbLog.AppendText($"{time}  {msg}\n");
        _rtbLog.ScrollToCaret();
    }

    #endregion

    #region 按钮事件

    private void OnNewTest(object? sender, EventArgs e)
    {
        // 检查是否有未保存的试验
        if (_ctx.CurrentTest != null && _ctx.CurrentTest.TotalTestTime > 0 && _ctx.CurrentTest.Flag != "10000000")
        {
            MessageBox.Show("当前存在已完成但未保存的试验记录，请先保存试验记录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new NewTestForm();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.CreatedTest != null)
        {
            _ctx.CurrentTest = dlg.CreatedTest;
            _lblSampleId.Text = $"样品：{dlg.CreatedTest.ProductId}";
            AddSystemMessage($"新建试验：{dlg.CreatedTest.ProductId} / {dlg.CreatedTest.TestId}");
            _ctx.Controller.ResetToIdle();
            _ctx.DaqWorker.ClearCsvBuffer();
        }
    }

    private void OnStartHeat(object? sender, EventArgs e)
    {
        if (_ctx.CurrentTest == null)
        {
            MessageBox.Show("请先新建试验。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_ctx.Controller.TryStartHeating())
        {
            AddSystemMessage("开始升温，系统升温中");
            UpdateButtonStates();
        }
    }

    private void OnStopHeat(object? sender, EventArgs e)
    {
        if (_ctx.Controller.TryStopHeating())
        {
            AddSystemMessage("停止升温，系统冷却中");
            UpdateButtonStates();
        }
    }

    private void OnStartRecord(object? sender, EventArgs e)
    {
        if (_ctx.Controller.TryStartRecording())
        {
            AddSystemMessage("开始记录，计时开始");
            UpdateButtonStates();
        }
    }

    private void OnStopRecord(object? sender, EventArgs e)
    {
        if (_ctx.Controller.TryStopRecording())
        {
            AddSystemMessage("用户手动停止记录");
            UpdateButtonStates();
        }
    }

    private void OnOpenRecord(object? sender, EventArgs e)
    {
        if (_ctx.CurrentTest == null)
        {
            MessageBox.Show("没有当前试验。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new PhenomRecordForm(_ctx.CurrentTest);
        if (dlg.ShowDialog() == DialogResult.OK && dlg.SavedTest != null)
        {
            AddSystemMessage("试验记录已保存");
            var saved = dlg.SavedTest;

            // 导出Excel和PDF
            var csvLines = _ctx.DaqWorker.GetCsvLines();
            var baseDir = _ctx.Configuration["FileStorage:BaseDirectory"] ?? @"D:\ISO11820";

            try
            {
                string excelPath = _ctx.Exporter.ExportExcel(saved, csvLines, baseDir);
                AddSystemMessage($"Excel报告已生成：{excelPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Excel报告生成失败");
                AddSystemMessage($"Excel报告失败：{ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault() ?? ""}", Color.Red);
            }

            try
            {
                if (_ctx.Configuration.GetValue<bool>("Report:EnablePdfExport", true))
                {
                    string pdfPath = _ctx.Exporter.ExportPdf(saved, csvLines, baseDir);
                    AddSystemMessage($"PDF报告已生成：{pdfPath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PDF报告生成失败");
                AddSystemMessage($"PDF报告失败：{ex.Message}", Color.Red);
            }

            _ctx.Controller.AfterSaveReturnToPreparing();
            AddSystemMessage("可以创建新试验或等待温度稳定后继续");
            UpdateButtonStates();
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        if (_ctx.CurrentTest == null || string.IsNullOrEmpty(_ctx.CurrentTest.TestId))
        {
            MessageBox.Show("没有可导出的试验。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var test = _ctx.Db.GetTest(_ctx.CurrentTest.ProductId, _ctx.CurrentTest.TestId);
        if (test == null || test.Flag != "10000000")
        {
            MessageBox.Show("试验记录尚未保存，请先保存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var csvLines = _ctx.DaqWorker.GetCsvLines();
        var baseDir = _ctx.Configuration["FileStorage:BaseDirectory"] ?? @"D:\ISO11820";

        try
        {
            string excelPath = _ctx.Exporter.ExportExcel(test, csvLines, baseDir);
            AddSystemMessage($"Excel报告已导出：{excelPath}");

            string pdfPath = _ctx.Exporter.ExportPdf(test, csvLines, baseDir);
            AddSystemMessage($"PDF报告已导出：{pdfPath}");

            MessageBox.Show($"报告已导出到：\n{excelPath}\n{pdfPath}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        MessageBox.Show("参数配置功能：可修改 appsettings.json 中的仿真参数（升温速率、目标温度、波动幅度等）。",
            "参数设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion

    #region 按钮状态控制

    private void UpdateButtonStates()
    {
        bool hasActiveTest = _ctx.CurrentTest != null;
        bool hasUnsaved = _ctx.CurrentTest != null && _ctx.CurrentTest.Flag != "10000000";

        _btnNewTest.Enabled = _currentStatus switch
        {
            TestStatus.Idle => true,
            TestStatus.Preparing => !hasActiveTest || hasActiveTest && _ctx.CurrentTest?.Flag == "10000000",
            TestStatus.Ready => false,
            TestStatus.Recording => false,
            TestStatus.Complete => !hasUnsaved,
            _ => false
        };

        _btnStartHeat.Enabled = _currentStatus == TestStatus.Idle && hasActiveTest;
        _btnStopHeat.Enabled = _currentStatus == TestStatus.Preparing || _currentStatus == TestStatus.Ready || _currentStatus == TestStatus.Complete;
        _btnStartRec.Enabled = _currentStatus == TestStatus.Ready;
        _btnStopRec.Enabled = _currentStatus == TestStatus.Recording;
        _btnRecord.Enabled = _currentStatus == TestStatus.Complete && hasUnsaved;
        _btnExport.Enabled = _ctx.CurrentTest != null && _ctx.CurrentTest.Flag == "10000000";
        _btnSettings.Enabled = _currentStatus == TestStatus.Idle
                            || _currentStatus == TestStatus.Preparing
                            || _currentStatus == TestStatus.Ready;
    }

    #endregion

    #region 历史查询事件

    private void OnSearchHistory(object? sender, EventArgs e)
    {
        var tests = _ctx.Db.QueryTests(
            _dtFrom.Value.Date,
            _dtTo.Value.Date.AddDays(1),
            string.IsNullOrWhiteSpace(_txtSearchPid.Text) ? null : _txtSearchPid.Text.Trim(),
            _cmbSearchOp.SelectedIndex > 0 ? _cmbSearchOp.SelectedItem?.ToString() : null
        );

        _dgvHistory.DataSource = null;
        _dgvHistory.DataSource = tests.Select(t => new
        {
            t.TestId,
            t.ProductId,
            试验日期 = t.TestDate.ToString("yyyy-MM-dd"),
            操作员 = t.Operator,
            t.AmbTemp,
            t.AmbHumi,
            试验前质量 = t.PreWeight,
            试验后质量 = t.PostWeight,
            失重率 = $"{t.LostWeightPer:F2}%",
            温升 = $"{t.DeltaTf:F1}℃",
            时长 = $"{t.TotalTestTime}s",
            是否保存 = t.Flag == "10000000" ? "已保存" : "未保存"
        }).ToList();
    }

    private void OnHistoryDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var testId = _dgvHistory.Rows[e.RowIndex].Cells["TestId"].Value?.ToString();
        var productId = _dgvHistory.Rows[e.RowIndex].Cells["ProductId"].Value?.ToString();

        if (testId == null || productId == null) return;

        var test = _ctx.Db.GetTest(productId, testId);
        if (test == null) return;

        var product = _ctx.Db.GetProduct(productId);

        var msg = $"试验编号：{test.TestId}\n" +
                  $"样品编号：{test.ProductId}\n" +
                  $"样品名称：{product?.ProductName ?? ""}\n" +
                  $"规格：{product?.Specific ?? ""}\n" +
                  $"操作员：{test.Operator}\n" +
                  $"试验日期：{test.TestDate:yyyy-MM-dd}\n" +
                  $"环境温度：{test.AmbTemp:F1}℃\n" +
                  $"环境湿度：{test.AmbHumi:F1}%\n" +
                  $"试验前质量：{test.PreWeight:F2}g\n" +
                  $"试验后质量：{test.PostWeight:F2}g\n" +
                  $"失重率：{test.LostWeightPer:F2}%\n" +
                  $"样品温升：{test.DeltaTf:F1}℃\n" +
                  $"火焰持续时间：{test.FlameDuration}s\n" +
                  $"时长：{test.TotalTestTime}s\n" +
                  $"备注：{test.Memo ?? ""}";

        MessageBox.Show(msg, "试验详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnExportHistoryExcel(object? sender, EventArgs e)
    {
        if (_dgvHistory.Rows.Count == 0)
        {
            MessageBox.Show("没有数据可导出，请先查询。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_dgvHistory.Rows[0].Cells["TestId"].Value == null) return;

        var testId = _dgvHistory.Rows[0].Cells["TestId"].Value.ToString()!;
        var productId = _dgvHistory.Rows[0].Cells["ProductId"].Value.ToString()!;
        var test = _ctx.Db.GetTest(productId, testId);
        if (test == null) return;

        var baseDir = _ctx.Configuration["FileStorage:BaseDirectory"] ?? @"D:\ISO11820";
        try
        {
            string path = _ctx.Exporter.ExportExcel(test, new List<string>(), baseDir);
            AddSystemMessage($"Excel导出：{path}");
            MessageBox.Show($"已导出到：{path}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportHistoryPdf(object? sender, EventArgs e)
    {
        if (_dgvHistory.Rows.Count == 0)
        {
            MessageBox.Show("没有数据可导出，请先查询。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_dgvHistory.Rows[0].Cells["TestId"].Value == null) return;

        var testId = _dgvHistory.Rows[0].Cells["TestId"].Value.ToString()!;
        var productId = _dgvHistory.Rows[0].Cells["ProductId"].Value.ToString()!;
        var test = _ctx.Db.GetTest(productId, testId);
        if (test == null) return;

        var baseDir = _ctx.Configuration["FileStorage:BaseDirectory"] ?? @"D:\ISO11820";
        try
        {
            string path = _ctx.Exporter.ExportPdf(test, new List<string>(), baseDir);
            AddSystemMessage($"PDF导出：{path}");
            MessageBox.Show($"已导出到：{path}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region 校准事件

    private void OnRecordCalibration(object? sender, EventArgs e)
    {
        var sensorData = _ctx.Simulator.GetSensorData();
        if (!sensorData.TryGetValue(16, out var calData))
        {
            MessageBox.Show("无法获取校准温度数据。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var record = new CalibrationRecord
        {
            Id = Guid.NewGuid().ToString(),
            CalibrationDate = DateTime.Now.ToString("o"),
            CalibrationType = "Surface",
            ApparatusId = 0,
            Operator = _ctx.CurrentOperatorName,
            TemperatureData = System.Text.Json.JsonSerializer.Serialize(new { Temp = calData.Value, Time = DateTime.Now.ToString("HH:mm:ss") }),
            PassedCriteria = 1,
            Remarks = "手动记录",
            CreatedAt = DateTime.Now.ToString("o")
        };

        _ctx.Db.SaveCalibrationRecord(record);
        AddSystemMessage($"校准数据已记录：{calData.Value:F1}℃");
        LoadCalibrationHistory();
    }

    private void LoadCalibrationHistory()
    {
        var records = _ctx.Db.GetCalibrationRecords();
        _dgvCalib.DataSource = null;
        _dgvCalib.DataSource = records.Select(r => new
        {
            r.Id,
            日期 = r.CalibrationDate,
            类型 = r.CalibrationType,
            操作员 = r.Operator,
            r.PassedCriteria,
            r.Remarks
        }).ToList();
    }

    #endregion
}
