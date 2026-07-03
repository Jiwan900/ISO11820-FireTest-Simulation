using ISO11820.Global;
using ISO11820.Models;

namespace ISO11820.Forms;

/// <summary>
/// 试验现象记录窗体
/// </summary>
public class PhenomRecordForm : Form
{
    private CheckBox _chkFlame;
    private NumericUpDown _numFlameTime;
    private NumericUpDown _numFlameDuration;
    private TextBox _txtPostWeight;
    private TextBox _txtMemo;
    private Label _lblPreWeight;
    private Button _btnSave, _btnCancel;
    private Label _lblError;

    private readonly TestMaster _test;
    public TestMaster? SavedTest { get; private set; }

    public PhenomRecordForm(TestMaster test)
    {
        _test = test;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "试验记录 — 现象与结果";
        Size = new Size(450, 440);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(245, 245, 250);

        int y = 15;

        // 试验前质量（只读）
        AddLabel($"样品编号：{_test.ProductId}", ref y);
        AddLabel($"试验编号：{_test.TestId}", ref y);
        AddLabel($"试验前质量：{_test.PreWeight:F2} g", ref y);

        y += 5;

        // 火焰复选框
        _chkFlame = new CheckBox
        {
            Text = "是否出现持续火焰",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            Location = new Point(20, y),
            Size = new Size(200, 28)
        };
        _chkFlame.CheckedChanged += (s, e) =>
        {
            bool enabled = _chkFlame.Checked;
            _numFlameTime.Enabled = enabled;
            _numFlameDuration.Enabled = enabled;
        };
        Controls.Add(_chkFlame);
        y += 33;

        // 火焰发生时刻
        var lblFt = new Label { Text = "火焰发生时刻 (秒)：", Font = new Font("Microsoft YaHei", 10), Location = new Point(35, y), AutoSize = true };
        _numFlameTime = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 99999,
            Location = new Point(175, y - 2),
            Size = new Size(100, 25),
            Enabled = false
        };
        Controls.AddRange([lblFt, _numFlameTime]);
        y += 30;

        // 火焰持续时间
        var lblFd = new Label { Text = "火焰持续时间 (秒)：", Font = new Font("Microsoft YaHei", 10), Location = new Point(35, y), AutoSize = true };
        _numFlameDuration = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 99999,
            Location = new Point(175, y - 2),
            Size = new Size(100, 25),
            Enabled = false
        };
        Controls.AddRange([lblFd, _numFlameDuration]);
        y += 35;

        // 试验后质量（必填）
        var lblPost = new Label
        {
            Text = "试验后质量 (g) *必填：",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            Location = new Point(20, y),
            AutoSize = true
        };
        _txtPostWeight = new TextBox
        {
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(200, y - 2),
            Size = new Size(100, 25)
        };
        Controls.AddRange([lblPost, _txtPostWeight]);
        y += 35;

        // 备注
        var lblMemo = new Label { Text = "备注：", Font = new Font("Microsoft YaHei", 10), Location = new Point(20, y), AutoSize = true };
        _txtMemo = new TextBox
        {
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(20, y + 22),
            Size = new Size(395, 25),
            Multiline = true,
            Height = 60
        };
        Controls.AddRange([lblMemo, _txtMemo]);
        y += 90;

        // 错误提示
        _lblError = new Label
        {
            Font = new Font("Microsoft YaHei", 9),
            ForeColor = Color.Red,
            Location = new Point(20, y),
            Size = new Size(400, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        Controls.Add(_lblError);
        y += 30;

        // 按钮
        _btnSave = new Button
        {
            Text = "保存试验记录",
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            Location = new Point(130, y),
            Size = new Size(130, 38),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += OnSave;
        Controls.Add(_btnSave);

        _btnCancel = new Button
        {
            Text = "取消",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(280, y),
            Size = new Size(80, 38),
            FlatStyle = FlatStyle.Flat
        };
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(_btnCancel);
    }

    private void AddLabel(string text, ref int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(20, y),
            AutoSize = true
        };
        Controls.Add(lbl);
        y += 25;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (!double.TryParse(_txtPostWeight.Text.Trim(), out double postWeight) || postWeight < 0)
        {
            _lblError.Text = "请输入有效的试验后质量";
            _lblError.Visible = true;
            return;
        }

        var ctx = AppRuntime.Instance;
        var simulator = ctx.Simulator;
        var controller = ctx.Controller;

        // 计算各通道最终值（从仿真引擎获取当前值）
        _test.PostWeight = postWeight;
        _test.LostWeight = _test.PreWeight - postWeight;
        _test.LostWeightPer = _test.LostWeight / _test.PreWeight * 100;
        _test.FlameTime = _chkFlame.Checked ? (int)_numFlameTime.Value : 0;
        _test.FlameDuration = _chkFlame.Checked ? (int)_numFlameDuration.Value : 0;
        _test.Memo = _txtMemo.Text.Trim();
        _test.PhenoCode = _chkFlame.Checked ? $"Flame:{_test.FlameTime}s/{_test.FlameDuration}s" : "NoFlame";

        // 温度统计数据
        _test.FinalTf1 = Math.Round(simulator.TF1, 1);
        _test.FinalTf2 = Math.Round(simulator.TF2, 1);
        _test.FinalTs = Math.Round(simulator.TS, 1);
        _test.FinalTc = Math.Round(simulator.TC, 1);
        _test.FinalTf1Time = _test.FinalTf2Time = _test.FinalTsTime = _test.FinalTcTime = controller.ElapsedSeconds;

        // 温升
        _test.DeltaTf1 = Math.Round(simulator.TF1 - _test.AmbTemp, 1);
        _test.DeltaTf2 = Math.Round(simulator.TF2 - _test.AmbTemp, 1);
        _test.DeltaTs = Math.Round(simulator.TS - _test.AmbTemp, 1);
        _test.DeltaTc = Math.Round(simulator.TC - _test.AmbTemp, 1);
        _test.DeltaTf = _test.DeltaTs; // 样品温升取表面温升

        _test.TotalTestTime = controller.ElapsedSeconds;
        _test.ConstPower = simulator.PidOutput;

        // 保存到数据库
        ctx.Db.UpdateTestResult(_test);
        ctx.CurrentTest = null;

        // 导出CSV
        var csvLines = ctx.DaqWorker.GetCsvLines();
        var baseDir = ctx.Configuration["FileStorage:BaseDirectory"] ?? @"D:\ISO11820";
        ctx.Exporter.ExportCsv(_test, csvLines, baseDir);

        SavedTest = _test;
        DialogResult = DialogResult.OK;
        Close();
    }
}
