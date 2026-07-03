using ISO11820.Core;
using ISO11820.Data;
using ISO11820.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ISO11820.Global;

/// <summary>
/// 全局应用上下文 — 单例，持有所有核心对象引用
/// </summary>
public class AppRuntime
{
    public static AppRuntime Instance { get; private set; } = null!;

    public IConfiguration Configuration { get; }
    public DbHelper Db { get; private set; } = null!;
    public TestController Controller { get; private set; } = null!;
    public DaqWorker DaqWorker { get; private set; } = null!;
    public SensorSimulator Simulator { get; private set; } = null!;
    public ExportService Exporter { get; private set; } = null!;

    /// <summary>
    /// 当前操作员信息（登录后设置）
    /// </summary>
    public string CurrentOperatorId { get; set; } = "";
    public string CurrentOperatorName { get; set; } = "";
    public string CurrentUserType { get; set; } = "";

    /// <summary>
    /// 当前试验信息
    /// </summary>
    public Models.TestMaster? CurrentTest { get; set; }

    /// <summary>
    /// PID输出值历史队列（用于恒功率计算）
    /// </summary>
    public Queue<int> PidHistory { get; } = new(600);

    private AppRuntime(IConfiguration config)
    {
        Configuration = config;
    }

    /// <summary>
    /// 初始化全局上下文
    /// </summary>
    public static void Initialize(IConfiguration config)
    {
        var ctx = new AppRuntime(config);
        Instance = ctx;

        // 1. 配置 Serilog 日志
        var logPath = Path.Combine(
            config["FileStorage:BaseDirectory"] ?? @"D:\ISO11820",
            "Logs",
            "iso11820-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("ISO 11820 系统启动");

        // 2. 初始化数据库
        var dbPath = config["Database:SqlitePath"] ?? @"Data\ISO11820.db";
        var fullDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);
        var dbDir = Path.GetDirectoryName(fullDbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        ctx.Db = new DbHelper(fullDbPath);
        ctx.Db.InitializeDatabase();

        // 3. 初始化核心对象
        ctx.Simulator = new SensorSimulator();
        ctx.Controller = new TestController();
        ctx.DaqWorker = new DaqWorker();
        ctx.Exporter = new ExportService();

        Log.Information("系统初始化完成");
    }
}
