using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyLanServer.Core.Interfaces;
using MyLanServer.Infrastructure.Data;
using MyLanServer.Infrastructure.Services;
using MyLanServer.Infrastructure.Web;
using MyLanServer.UI.ViewModels;
using MyLanServer.UI.Views;
using Serilog;

namespace MyLanServer;

public static class Program
{
    [STAThread] // WPF 必须要求 STA 线程
    public static void Main(string[] args)
    {
        // 1. 配置 Serilog (写入 logs 文件夹)
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            // 2. 构建 Host
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog() // 使用 Serilog 接管 Microsoft.Extensions.Logging
                .ConfigureServices((context, services) =>
                {
                    // --- Core Infrastructure ---
                    services.AddSingleton<DapperContext>();
                    services.AddScoped<ITaskRepository, TaskRepository>();
                    services.AddScoped<IDepartmentRepository, DepartmentRepository>();
                    services.AddScoped<IPersonRepository, PersonRepository>();
                    services.AddTransient<ISubmissionService, SubmissionService>();
                    services.AddTransient<IExcelMergeService, ExcelMergeService>();
                    services.AddSingleton<IWebServerService, WebServerManager>();
                    services.AddSingleton<IPasswordHashService, PasswordHashService>();
                    services.AddSingleton<IAttachmentService, AttachmentService>();
                    services.AddSingleton<ITaskAttachmentService, TaskAttachmentService>();
                    services.AddSingleton<IFileExtensionService, FileExtensionService>();
                    services.AddSingleton<ISlugGeneratorService, SlugGeneratorService>();
                    services.AddSingleton<IExpiryQuickOptionsService, ExpiryQuickOptionsService>();
                    services.AddSingleton<IIoLockService, IoLockService>();
                    services.AddSingleton<IFileSystemService, FileSystemService>();
                    services.AddSingleton<IFileValidationService, FileValidationService>();
                    services.AddSingleton<IIdCardValidationService, IdCardValidationService>();

                    // --- UI Services (WPF) ---
                    services.AddSingleton<App>(); // 注册 App 自身
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<TaskConfigViewModel>();
                    services.AddTransient<DepartmentViewModel>();
                    services.AddTransient<PersonViewModel>();
                    services.AddTransient<MergeDialogViewModel>();
                    services.AddTransient<SubmissionListViewModel>();
                    services.AddTransient<ColumnSelectorViewModel>();
                    services.AddTransient<TaskConfigDialog>();
                    services.AddTransient<DepartmentManagementDialog>();
                    services.AddTransient<PersonManagementDialog>();
                    services.AddTransient<MergeDialog>();
                    services.AddTransient<SubmissionListDialog>();
                    services.AddTransient<ColumnSelectorDialog>();
                })
                .Build();

            // 3. 启动 WPF 应用
            var app = host.Services.GetRequiredService<App>();
            app.InitHost(host); // 将 Host 传递给 App
            app.Run(); // 启动消息循环
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}