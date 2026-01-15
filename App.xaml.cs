using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyLanServer.Infrastructure.Data;
using MyLanServer.UI.Views;

namespace MyLanServer;

public partial class App
{
    public App()
    {
        // !!! 必须调用这行代码来加载 App.xaml 中的资源 !!!
        InitializeComponent();
    }

    public static IHost? AppHost { get; private set; }

    public static IServiceProvider ServiceProvider =>
        AppHost?.Services ?? throw new InvalidOperationException("AppHost is not initialized");

    // 我把这个方法改名为 InitHost，避免跟系统生成的 InitializeComponent 混淆
    public void InitHost(IHost host)
    {
        AppHost = host;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 注意：不要调用 base.OnStartup(e)，因为我们移除了 StartupUri
        // base.OnStartup(e); 

        if (AppHost == null) return;

        await AppHost.StartAsync();

        // 1. 初始化数据库
        var dbContext = AppHost.Services.GetRequiredService<DapperContext>();
        await dbContext.InitDatabaseAsync();

        // 2. 启动主窗口
        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (AppHost != null)
        {
            await AppHost.StopAsync(TimeSpan.FromSeconds(5));
            AppHost.Dispose();
        }

        base.OnExit(e);
    }
}