global using LanguageExt;
global using static LanguageExt.Prelude;
using LinxUniverse.Auth;
using LinxUniverse.CST;
using LinxUniverse.DI;
using LinxUniverse.ProcessManager;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TrayScanStandard.Data;
using TrayScanStandard.Jobs;
using TrayScanStandard.Mediator.Behaviors;
using TrayScanStandard.Service;
using TrayScanStandard.Services;
using TrayScanStandard.View;
using TrayScanStandard.View.CZPallet;
using TrayScanStandard.ViewModel;
using TrayScanStandard.ViewModel.CZPallet;
using TrayScanStandard.Utils;
using VMWebAIClient;

namespace TrayScanStandard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IHost Host
        {
            get;
        }

        public static T GetService<T>()
            where T : class
        {

            System.Diagnostics.Debug.WriteLine("----------------------------");
            System.Diagnostics.Debug.WriteLine(typeof(T).ToString());
            //var a = typeof(T);
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} 需要在App.xaml.cs.的ConfigureServices中进行注册。");
            }

            return service;
        }

        public App()
        {
            string MName = System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName;
            string PName = System.IO.Path.GetFileNameWithoutExtension(MName);
            System.Diagnostics.Process[] myProcess = System.Diagnostics.Process.GetProcessesByName(PName);

            if (myProcess.Length > 1)
            {
                MessageBox.Show("本程序一次只能运行一个实例！", "提示");
                Application.Current.Shutdown();
                return;
            }


                        // 提前初始化文件日志，确保 InitCST 等启动日志能被记录（此时还未获取 RichTextBox，仅写文件）
            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
              .MinimumLevel.Override("Quartz", Serilog.Events.LogEventLevel.Information)
              .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Information)
              .CreateLogger();

            MainStorage.Init();
            //if (MainStorage.Saves.Stage == null)
            //{
            //    MainStorage.Saves.Stage = new();
            //    MainStorage.SaveManager.Save();
            //}
            //DB.Database.Migrate();
            Host = Microsoft.Extensions.Hosting.Host.
                     CreateDefaultBuilder().
                     UseContentRoot(AppContext.BaseDirectory).
                     ConfigureServices((context, services) =>
                     {
                         //services.AddSingleton<ServoManagerViewModel>();
                         //services.AddTransient<ServoManagerView>();


                         services.AddQuartz(config =>
                         {
                             JobKey jobKey = JobKey.Create(nameof(DailyJobs));
                             config.AddJob<DailyJobs>(jobKey).AddTrigger(trigger =>
                             {
                                 trigger.ForJob(jobKey).WithCronSchedule("0 10 0 * * ?");
                             });

                             JobKey jobKey1 = JobKey.Create(nameof(HourJobs));
                             config.AddJob<HourJobs>(jobKey1).AddTrigger(trigger =>
                             {
                                 trigger.ForJob(jobKey1).WithCronSchedule("0 10 * * * ?");
                             });

                             // 【已移除心跳】
                             // JobKey heartbeatJobKey = JobKey.Create(nameof(HeartbeatJobs));
                             // config.AddJob<HeartbeatJobs>(heartbeatJobKey).AddTrigger(trigger =>
                             // {
                             //     trigger.ForJob(heartbeatJobKey)
                             //         .StartNow()
                             //         .WithSimpleSchedule(x => x.WithIntervalInSeconds(5).RepeatForever());
                             // });

                         });

                         services.AddQuartzHostedService(config =>
                         {
                             config.WaitForJobsToComplete = true;
                         });

                         // 原实现（保留）：
                         // services.AddSingleton<LinxAuthenticationStateProvider, StandLinxAuthenticationStateProvider<LinxUser>>();
                         // 新实现：使用项目内Provider，避免基线库15分钟重验证导致强制锁屏
                         services.AddSingleton<LinxAuthenticationStateProvider, TrayScanAuthenticationStateProvider>();

                         services.AddAuth<LinxUser>();

                         services.AddSingleton<MainViewModel>();
                         services.AddTransient<MainWindow>();

                         services.AddTransient<PalletLogViewModel>();
                         services.AddTransient<PalletLogView>();

                         services.AddSingleton<SettingViewModel>();
                         services.AddTransient<SettingView>();

                         services.AddSingleton<UserManagerViewModel>();
                         services.AddTransient<UserManagerView>();

                         services.AddSingleton<BatteryManagerViewModel>();
                         services.AddTransient<BatteryManager>();

                         services.AddTransient<WCSLogViewModel>();
                         services.AddTransient<WCSLogView>();
                         
                         services.AddTransient<LightManagerView>();
                         services.AddTransient<LightManagerViewModel>();

                         // 光源服务（Wordop 使用单例 + 启动自动连接）
                         services.AddSingleton<WordopLightService>();
                         services.AddSingleton<CognexLightService>();

                         services.AddTransient<ImageDisplayView>();
                         services.AddSingleton<ImageDisplayViewModel>();

                         services.AddTransient<AllBcrListView>();
                         //services.AddSingleton<YWStageViewViewModels>();
                         services.AddTransient<YWStageView>();


                         services.AddTransient<LogDashBoardView>();


                         services.AddSingleton<RichTextBox>();
                         services.AddSingleton<CacheService>();

                         services.AddSingleton<ScanCameraService>();
                         services.AddSingleton<WcsTrayScanStandardServer>();
                        services.AddSingleton<PlcModbusHealthService>();

                        services.AddTransient<StationSettingView>();


                         // 注入光源CST服务
                         services.AddCstService();


                         services.AddVMWebAIClient(client =>
                         {
                             client.BaseAddress = new Uri("http://localhost:54567/");
                             // other HttpClient configuration
                         });
                         services.AddDbContext<LinxUserDBContext<LinxUser>, LinxContext>(option =>
                         {
                             option.UseSqlite(context.Configuration.GetConnectionString("LinxContextConnectionSqlite"));
                         }, ServiceLifetime.Transient, ServiceLifetime.Transient);


                         services.AddMediatR(cfg =>
                         {
                             cfg.RegisterServicesFromAssemblyContaining<App>();

                         });

                         services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
                         //services.AddSwaggerGen(
                         //);

                     })
                     .ConfigureLogging(logging =>
                     {
                         logging.ClearProviders();
                         logging.AddSerilog(dispose: true);
                         //logging.AddConsole();
                         //logging.AddDebug();
                     })
                     .ConfigureWebHostDefaults(webHostBuilder =>
                     {
                        
                         webHostBuilder.UseStartup<Startup>();
                         // 原实现（保留）：
                         // webHostBuilder.UseUrls($"http://*:{MainViewModel.Saves.WcsHeartPort}");
                         // 新实现：程序内部WebHost使用独立端口，避免与WCS心跳Socket端口冲突导致“自连接在线”
                         const int localApiPort = 18080;
                         webHostBuilder.UseUrls($"http://*:{localApiPort}");
                         webHostBuilder.ConfigureKestrel((context, options) =>
                         {
                             // Handle requests up to 50 MB
                             options.Limits.MaxRequestBodySize = 16_000_000_000_000_000;
                             options.Limits.KeepAliveTimeout = TimeSpan.FromHours(10);
                         });

                     })
                     .ConfigureServices(services =>
                     {
                         services.Configure<FormOptions>(options =>
                         {
                             // Set the limit to 256 MB
                             options.MultipartBodyLengthLimit = 2684354560;
                         });
                     })
                     .
                     Build();
            InitContext();
            //Host.UseSwagger();
            //Host.UseSwaggerUI(options => {
            //    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            //    options.RoutePrefix = string.Empty;
            //});
            RichTextBox aa = GetService<RichTextBox>();
            aa.IsReadOnly = true;
            aa.Background = System.Windows.Media.Brushes.Black;
            aa.TextChanged += Aa_TextChanged;

            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
                                .MinimumLevel.Override("Quartz", Serilog.Events.LogEventLevel.Information)
                                .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Information)
              // 软件名字
              .WriteTo.File("logs/TrayScanStandard.NET.log", rollingInterval: RollingInterval.Day,
              outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] |{SourceContext}.{Method}| - {Message}{NewLine}{Exception}")
              .WriteTo.RichTextBox(GetService<RichTextBox>()).Enrich.FromLogContext()
              .CreateLogger();


            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Host created.");

            manager = ProcessManagerFactory.Create();
            var config = ProcessManagerFactory.CreateProcessConfig(
                id: "vmapi",
                name: "vm算法api",
                executablePath: @"vmapi\LinxUniverse.VM.Webapi.exe"
            );

            // 用系统指令启动@"vmapi\LinxUniverse.VM.Webapi.exe"



            manager.RegisterProcess(config);

            var res =  manager.StartProcessAsync("vmapi").Result;
            Host.Start();

            try
            {
                GetService<ScanCameraService>().Init();
                logger.LogInformation("启动初始化：已触发相机自动连接");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启动初始化：相机自动连接触发失败");
            }
        }
        static ProcessManager manager;
        private static void InitContext()
        {
            LinxContext DB = GetService<LinxContext>();
            // DB.Database.EnsureCreated();
            DB.Database.Migrate();
            BatteryRoiJsonStore.SyncToDatabase(DB);
            //
            //foreach (var batteryInfo in DB.BatteryInfos.ToList())
            //{
            //    batteryInfo.Param = new();
            //}

            //DB.SaveChanges();
        }
        private void Aa_TextChanged(object sender, TextChangedEventArgs e)
        {
            var rb = (sender as RichTextBox);
            if (e.Changes.First().AddedLength < 5) return;
            var MaxLines = 100;
            //rb.Document.Blocks.LastBlock.
            // 获得rb的行数
            EndBlock(rb);
            var lines = rb.Document.Blocks.Count;
            if (lines > MaxLines)
            {
                // 获得第一行的TextPointer
                // var start = rb.Document.Blocks.FirstBlock.ContentStart;
                // 删除第一行到第MaxLines行
                rb.Document.Blocks.Remove(rb.Document.Blocks.FirstBlock);
            }
            //if (rb.)
        }
        private void EndBlock(RichTextBox richTextBox)
        {
            var paragraph = new Paragraph();
            richTextBox.Document.Blocks.Add(paragraph);
            richTextBox.CaretPosition = paragraph.ContentEnd;
            richTextBox.ScrollToEnd();
            //  richTextBox.Document.Blocks.Remove(paragraph);
            //var position = richTextBox.CaretPosition.GetLineStartPosition(-1);
            //if (position != null)
            //{
            //    richTextBox.CaretPosition = position;
            //}
        }

        protected override void OnExit(ExitEventArgs e)
        {

            manager?.Dispose();
        }
    }
    

}
