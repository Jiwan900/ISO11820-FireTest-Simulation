using ISO11820.Forms;
using ISO11820.Global;
using Microsoft.Extensions.Configuration;

namespace ISO11820;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 初始化全局上下文
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        AppRuntime.Initialize(config);

        // 启动登录窗体
        Application.Run(new LoginForm());
    }
}
