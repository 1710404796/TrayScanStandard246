using Humanizer;
using LinxUniverse.Auth;
using LinxUniverse.CST;
using LinxUniverse.DI;
using LinxUniverse.Utils;
using LinxUniverse.VM;
using MediatR;
using Microsoft.Extensions.Logging;
using MugenCodeDetecter;
using RPDelectPallet.Meditor.Queries;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Models;
using TrayScanStandard.PLC;
using TrayScanStandard.Service;
using TrayScanStandard.Services;
using TrayScanStandard.ViewModel;
using VMWebAIClient;
// using static SerialCommunicate.ASCII_Data; // 未实际调用ASCII_Data成员，保留注释供回溯

namespace TrayScanStandard.Mediator.Handlers
{
    /// <summary>
    /// 初始化命令处理器
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="logger"></param>
    /// <param name="role"></param>
    public class InitMeCommandHandler(IMediator mediator,
        ILogger<InitMeCommandHandler> logger,
        RoleManager<LinxRole, LinxUser> role, 
        ScanCameraService scanCameraService,
        WcsTrayScanStandardServer  wcsTrayScanStandardServer,
        PlcModbusHealthService plcModbusHealthService,
        LinxContext linxContext,
        CacheService cacheService,
        IVMWebAIClient vmWebAIClient, 
        WordopLightService wordopLightService)
        : IRequestHandler<InitMeCommand>
    {
        public async Task Handle(InitMeCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 创建必要的目录
                FilenameHelper.CreateDir("DataFrame");
                FilenameHelper.CreateDir("InsertLog");
                FilenameHelper.CreateDir("Data2D");

                // 初始化权限表
                InitializePowerTable();

                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                MainStorage.SelectBattery = linxContext.BatteryTypeInfos.FirstOrDefault(s => s.Id == MainStorage.Saves.SelectBatteryId);

                // 初始化角色
                await InitializeRoles(cancellationToken);

                // 初始化光源设备
                var lightInitializationResult = await InitializeLights(cancellationToken);
                if (!lightInitializationResult.IsSuccess)
                {
                    logger.LogWarning("光源初始化失败，继续启动（拍照时将跳过光源控制）: {Message}", lightInitializationResult.ErrorMessage);
                }

                // 自动连接Wordop光源
                await TryAutoConnectWordopAsync(mediator, logger, wordopLightService, cancellationToken);

                // 初始化扫描相机服务
                scanCameraService.Init();

                // 初始化算法
                var algoInitializationResult = await InitializeAlgorithms(cancellationToken);
                if (!algoInitializationResult.IsSuccess)
                {
                    logger.LogError(algoInitializationResult.ErrorMessage);
                    MessageBox.Show(algoInitializationResult.ErrorMessage, "算法加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }


                // 触发WCS Socket服务在启动阶段初始化（构造函数内部已完成连接）
                _ = wcsTrayScanStandardServer;

                plcModbusHealthService.StartMonitoring(isConnected =>
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher == null)
                    {
                        return;
                    }

                    dispatcher.Invoke(() =>
                    {
                        // 原注释（保留参考）：
                        // PlcIsRunning = XcplcService.IsPlcRunning;
                        // 新增：将Modbus TCP连通状态同步到主界面PLC状态灯
                        App.GetService<MainViewModel>().PlcIsRunning = isConnected;
                    });
                });


                if (MainStorage.Saves.LightInfos.Length > 0)
                {
                    logger.LogInformation("所有初始化步骤完成");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "初始化过程中发生异常");
                MessageBox.Show($"初始化失败: {ex.Message}", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializePowerTable()
        {
            // 权限
            foreach (var item in Enum.GetValues<PowerEnum>())
            {
                if (!MainStorage.Saves.PowerTable.ContainsKey(item))
                {
                    MainStorage.Saves.PowerTable[item] = [];
                }

                // 角色
                foreach (var item1 in Enum.GetValues<RoleEnum>())
                {
                    if (!MainStorage.Saves.PowerTable[item].ContainsKey(item1))
                    {
                        MainStorage.Saves.PowerTable[item][item1] = false;
                    }
                }
            }
        }

        private async Task InitializeRoles(CancellationToken cancellationToken)
        {
            foreach (var roleName in Enum.GetNames<RoleEnum>())
            {
                await role.CreateAsync(new LinxRole { RoleName = roleName });
            }
        }

        private async Task<(bool IsSuccess, string ErrorMessage)> InitializeLights(CancellationToken cancellationToken)
        {
            try
            {
                // MainStorage.InitCST() 已在 App 构造函数中执行过。
                // 如果 MainStorage.CST 已有已连接的光源，则跳过重复连接，避免串口占用冲突。
                if (MainStorage.CST.Any())
                {
                    logger.LogInformation("MainStorage.CST 已有已连接的光源，跳过重复初始化");
                    return (true, "");
                }

                MainStorage.CST = await MainStorage.Saves.LightInfos.Map(
                    async s =>
                    {
                        // 将字符串形式的Com 解析为枚举 SerialPortType
                        Enum.TryParse<SerialPortType>(s.Com, out var com);

                        // 发送命令创建光源对象，并获取ComID
                        var g = await mediator.Send(new CreateCSTLightCommand(Com: com, ControllerType: Enum.Parse<LightControllerType>(s.ControllerType)));

                        // 查询并返回完整的灯光信息
                        return await mediator.Send(new GetLightQuery(g));
                    }).TraverseSerial(s => s!);

                if (MainStorage.CST == null)
                {
                    return (false, "光源初始化失败");
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "光源初始化异常");
                return (false, $"光源初始化失败: {ex.Message}");
            }
        }

        private async Task<(bool IsSuccess, string ErrorMessage)> InitializeAlgorithms(CancellationToken cancellationToken)
        {
            try
            {
                // 这里应该使用配置文件或常量来指定sol文件路径，而不是硬编码
                var solutionPath = Path.Combine(FilenameHelper.AppPath, "test.sol"); // 更好的默认路径
                var algores = await vmWebAIClient.CreateAlgoAsync(solutionPath, LinxUniverse.Algo.Common.DetectType.VisionMaster);

                // 检查各个算法初始化结果
                var validationResults = ValidateAlgorithmInitialization(algores, MainStorage.Algo, MainStorage.AlgoCnn);

                if (!validationResults.All(r => r.IsValid))
                {
                    var errorMessages = string.Join("\n", validationResults.Where(r => !r.IsValid).Select(r => r.ErrorMessage));
                    return (false, errorMessages);
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "算法初始化异常");
                return (false, $"算法初始化失败: {ex.Message}");
            }
        }

        private List<(bool IsValid, string ErrorMessage)> ValidateAlgorithmInitialization(
            Either<string, object> algoRes,
            Either<string, object> algo,
            Either<string, object> algoCnn)
        {
            var results = new List<(bool IsValid, string ErrorMessage)>();

            // 验证主要算法
            algoRes.IfLeft(error =>
            {
                results.Add((false, $"算法加载错误\n{error}"));
            });

            // 验证VisionMaster算法
            algo.IfLeft(error =>
            {
                results.Add((false, $"算法加载错误\n{error}"));
            });

            // 验证CNN算法
            algoCnn.IfLeft(error =>
            {
                results.Add((false, $"cnn算法加载错误\n{error}"));
            });

            return results;
        }

        private static async Task TryAutoConnectWordopAsync(
            IMediator mediator,
            ILogger logger,
            WordopLightService wordopLightService,
            CancellationToken cancellationToken)
        {
            try
            {
                var configDirectory = Path.Combine(AppContext.BaseDirectory, "Config");
                var globalConfigPath = Path.Combine(configDirectory, "GlobalConfig.json");
                var wordopConfigPath = Path.Combine(configDirectory, "WordopConfig.json");

                LightType? lastType = null;
                if (File.Exists(globalConfigPath))
                {
                    var globalJson = await File.ReadAllTextAsync(globalConfigPath, cancellationToken);
                    var global = JsonSerializer.Deserialize<GlobalConfig>(globalJson);
                    if (global != null)
                        lastType = global.LastLightType;
                }

                if (lastType.HasValue && lastType.Value != LightType.Wordop)
                    return;

                if (!File.Exists(wordopConfigPath))
                    return;

                var json = await File.ReadAllTextAsync(wordopConfigPath, cancellationToken);
                var config = JsonSerializer.Deserialize<WordopConfig>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.LastComPort))
                    return;

                var baudRate = config.LastBaudRate > 0 ? config.LastBaudRate : 19200;

                logger.LogInformation("Wordop 光源启动自动连接：{ComPort} / {BaudRate}", config.LastComPort, baudRate);

                bool ok = await wordopLightService.ConnectAsync(config.LastComPort, baudRate);
                if (ok)
                {
                    logger.LogInformation("Wordop 光源启动自动连接成功：{ComPort}", config.LastComPort);
                }
                else
                {
                    var msg = $"Wordop 光源启动自动连接失败：{config.LastComPort} / {baudRate}";
                    logger.LogError(msg);
                    await mediator.Send(new AddToWarningCommand(msg), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wordop 光源启动自动连接异常");
            }
        }

        private sealed class GlobalConfig
        {
            public LightType LastLightType { get; set; }
        }

        private sealed class WordopConfig
        {
            public string LastComPort { get; set; } = string.Empty;
            public int LastBaudRate { get; set; }
        }
    }
}


