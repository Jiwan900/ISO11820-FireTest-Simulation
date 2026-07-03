using ISO11820.Global;
using ISO11820.Models;

namespace ISO11820.Forms;

/// <summary>
/// 新建试验窗体
/// </summary>
public class NewTestForm : Form
{
    private TextBox _txtSampleId, _txtSampleName, _txtSpec, _txtDiameter, _txtHeight;
    private TextBox _txtAmbTemp, _txtAmbHumi, _txtPreWeight;
    private RadioButton _rbStandard, _rbCustom;
    private NumericUpDown _numCustomMinutes;
    private Label _lblOperator, _lblApparatus;
    private Button _btnCreate, _btnCancel;
    private Label _lblError;

    public TestMaster? CreatedTest { get; private set; }

    public NewTestForm()
    {
        InitializeComponent();
        LoadDeviceInfo();
    }

    private void InitializeComponent()
    {
        Text = "新建试验";
        Size = new Size(480, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(245, 245, 250);

        int y = 15;
        int leftX = 20;
        int fieldW = 200;

        // 样品编号
        y = AddField("样品编号：", ref y, out _txtSampleId, "如 20240613-001");
        _txtSampleId.Text = DateTime.Now.ToString("yyyyMMdd") + "-001";

        // 样品名称
        y = AddField("样品名称：", ref y, out _txtSampleName, "如 岩棉隔热板");

        // 规格型号
        y = AddField("规格型号：", ref y, out _txtSpec, "如 100×50×25mm");

        // 直径
        y = AddField("直径 (mm)：", ref y, out _txtDiameter, "如 100");

        // 高度
        y = AddField("高度 (mm)：", ref y, out _txtHeight, "如 50");

        y += 5;

        // 试验时长模式
        var groupMode = new GroupBox
        {
            Text = "试验时长模式",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(leftX, y),
            Size = new Size(430, 55)
        };

        _rbStandard = new RadioButton
        {
            Text = "标准 60 分钟",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(15, 22),
            Size = new Size(130, 25),
            Checked = true
        };
        _rbStandard.CheckedChanged += (s, e) =>
        {
            _numCustomMinutes.Enabled = _rbCustom.Checked;
        };

        _rbCustom = new RadioButton
        {
            Text = "自定义时长：",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(160, 22),
            Size = new Size(110, 25)
        };

        _numCustomMinutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 600,
            Value = 10,
            Location = new Point(275, 20),
            Size = new Size(70, 25),
            Enabled = false
        };

        var lblMin = new Label
        {
            Text = "分钟",
            Location = new Point(350, 22),
            AutoSize = true
        };

        groupMode.Controls.AddRange([_rbStandard, _rbCustom, _numCustomMinutes, lblMin]);
        Controls.Add(groupMode);
        y += 65;

        // 环境信息
        y += 5;
        y = AddField("环境温度 (℃)：", ref y, out _txtAmbTemp, "25.0");
        y = AddField("环境湿度 (%)：", ref y, out _txtAmbHumi, "50.0");
        y = AddField("试验前质量 (g)：", ref y, out _txtPreWeight, "如 100.00");

        y += 5;

        // 自动带入信息
        _lblOperator = new Label
        {
            Text = $"操作员：{AppRuntime.Instance.CurrentOperatorName}",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(leftX, y),
            AutoSize = true
        };
        Controls.Add(_lblOperator);
        y += 25;

        _lblApparatus = new Label
        {
            Text = "设备：加载中...",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(leftX, y),
            AutoSize = true
        };
        Controls.Add(_lblApparatus);
        y += 35;

        // 错误提示
        _lblError = new Label
        {
            Font = new Font("Microsoft YaHei", 9),
            ForeColor = Color.Red,
            Location = new Point(leftX, y),
            Size = new Size(430, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        Controls.Add(_lblError);
        y += 30;

        // 按钮
        _btnCreate = new Button
        {
            Text = "创建试验",
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            Location = new Point(140, y),
            Size = new Size(120, 38),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnCreate.FlatAppearance.BorderSize = 0;
        _btnCreate.Click += OnCreate;
        Controls.Add(_btnCreate);

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

    private int AddField(string label, ref int y, out TextBox textBox, string placeholder)
    {
        var lbl = new Label
        {
            Text = label,
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(20, y),
            AutoSize = true
        };

        textBox = new TextBox
        {
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(180, y - 2),
            Size = new Size(250, 25),
            PlaceholderText = placeholder
        };

        Controls.Add(lbl);
        Controls.Add(textBox);
        y += 32;
        return y;
    }

    private void LoadDeviceInfo()
    {
        var apparatus = AppRuntime.Instance.Db.GetApparatus();
        if (apparatus != null)
        {
            _lblApparatus.Text = $"设备：{apparatus.ApparatusName} ({apparatus.InnerNumber}) | 检定至：{apparatus.CheckDateT:yyyy-MM-dd}";
        }
    }

    private void OnCreate(object? sender, EventArgs e)
    {
        // 验证
        if (string.IsNullOrWhiteSpace(_txtSampleId.Text))
        {
            ShowError("请输入样品编号");
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtSampleName.Text))
        {
            ShowError("请输入样品名称");
            return;
        }
        if (!double.TryParse(_txtPreWeight.Text, out double preWeight) || preWeight <= 0)
        {
            ShowError("请输入有效的试验前质量");
            return;
        }
        if (!double.TryParse(_txtAmbTemp.Text, out double ambTemp))
        {
            ShowError("请输入有效的环境温度");
            return;
        }
        if (!double.TryParse(_txtAmbHumi.Text, out double ambHumi))
        {
            ShowError("请输入有效的环境湿度");
            return;
        }

        var ctx = AppRuntime.Instance;
        var apparatus = ctx.Db.GetApparatus();
        if (apparatus == null)
        {
            ShowError("设备信息未初始化");
            return;
        }

        // 生成试验ID
        string testId = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        // 保存样品信息
        var product = new ProductMaster
        {
            ProductId = _txtSampleId.Text.Trim(),
            ProductName = _txtSampleName.Text.Trim(),
            Specific = _txtSpec.Text.Trim(),
            Diameter = double.TryParse(_txtDiameter.Text, out double d) ? d : 0,
            Height = double.TryParse(_txtHeight.Text, out double h) ? h : 0
        };
        ctx.Db.UpsertProduct(product);

        // 创建试验记录
        var test = new TestMaster
        {
            ProductId = product.ProductId,
            TestId = testId,
            TestDate = DateTime.Now,
            AmbTemp = ambTemp,
            AmbHumi = ambHumi,
            According = "ISO 11820:2022",
            Operator = ctx.CurrentOperatorName,
            ApparatusId = apparatus.ApparatusId.ToString(),
            ApparatusName = apparatus.ApparatusName,
            ApparatusChkDate = apparatus.CheckDateT,
            RptNo = product.ProductId,
            PreWeight = preWeight
        };

        // 设置时长模式
        if (_rbCustom.Checked)
        {
            ctx.Controller.DurationMode = TestDurationMode.Custom;
            ctx.Controller.TargetDurationSeconds = (int)_numCustomMinutes.Value * 60;
        }
        else
        {
            ctx.Controller.DurationMode = TestDurationMode.Standard60Min;
            ctx.Controller.TargetDurationSeconds = 0;
        }

        ctx.Db.InsertTest(test);
        ctx.CurrentTest = test;
        CreatedTest = test;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowError(string msg)
    {
        _lblError.Text = msg;
        _lblError.Visible = true;
    }
}
