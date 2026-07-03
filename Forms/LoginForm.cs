using ISO11820.Global;

namespace ISO11820.Forms;

/// <summary>
/// 登录窗体
/// </summary>
public class LoginForm : Form
{
    private RadioButton _rbAdmin;
    private RadioButton _rbOperator;
    private TextBox _txtPassword;
    private Button _btnLogin;
    private Label _lblTitle;
    private Label _lblError;

    public LoginForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "ISO 11820 试验系统 - 登录";
        Size = new Size(420, 320);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(240, 240, 245);

        // 标题
        _lblTitle = new Label
        {
            Text = "ISO 11820 建筑材料不燃性试验系统",
            Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 80),
            AutoSize = false,
            Size = new Size(380, 35),
            Location = new Point(20, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // 角色分组
        var groupRole = new GroupBox
        {
            Text = "选择角色",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(40, 70),
            Size = new Size(330, 60)
        };

        _rbAdmin = new RadioButton
        {
            Text = "管理员 (admin)",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(20, 25),
            Size = new Size(140, 25),
            Checked = true
        };

        _rbOperator = new RadioButton
        {
            Text = "试验员 (experimenter)",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(170, 25),
            Size = new Size(160, 25)
        };

        groupRole.Controls.AddRange([_rbAdmin, _rbOperator]);

        // 密码
        var lblPwd = new Label
        {
            Text = "访问口令：",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(40, 145),
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(130, 142),
            Size = new Size(200, 25),
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };
        _txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };

        // 登录按钮
        _btnLogin = new Button
        {
            Text = "登 录",
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            Location = new Point(130, 185),
            Size = new Size(160, 38),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += (s, e) => DoLogin();

        // 错误提示
        _lblError = new Label
        {
            Font = new Font("Microsoft YaHei", 9, FontStyle.Regular),
            ForeColor = Color.Red,
            Location = new Point(40, 235),
            Size = new Size(340, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        Controls.AddRange([_lblTitle, groupRole, lblPwd, _txtPassword, _btnLogin, _lblError]);
    }

    private void DoLogin()
    {
        string username = _rbAdmin.Checked ? "admin" : "experimenter";
        string pwd = _txtPassword.Text.Trim();

        if (string.IsNullOrEmpty(pwd))
        {
            ShowError("请输入密码");
            return;
        }

        if (AppRuntime.Instance.Db.Login(username, pwd, out string userId, out string userType))
        {
            AppRuntime.Instance.CurrentOperatorId = userId;
            AppRuntime.Instance.CurrentOperatorName = username;
            AppRuntime.Instance.CurrentUserType = userType;

            Hide();
            var mainForm = new MainForm();
            mainForm.FormClosed += (s, e) => Close();
            mainForm.Show();
        }
        else
        {
            ShowError("密码错误，请重新输入");
            _txtPassword.SelectAll();
            _txtPassword.Focus();
        }
    }

    private void ShowError(string msg)
    {
        _lblError.Text = msg;
        _lblError.Visible = true;
    }
}
