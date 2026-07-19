using CommunityToolkit.Mvvm.Messaging;
using LinxUniverse.DI;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Messages;
using TrayScanStandard.Models;
using TrayScanStandard.Models.CZPallet;
using TrayScanStandard.Service.Models;

namespace TrayScanStandard.Service
{
    /// <summary>
    /// 物流线 Modbus TCP 服务：
    /// 1) 监测连通状态
    /// 2) 监听触发信号并执行“拍照->解码->回传WCS”
    /// </summary>
    public class PlcModbusHealthService
    {
        private readonly ILogger<PlcModbusHealthService> _logger;
        private readonly CacheService _cacheService;
        private readonly IMediator _mediator;
        private readonly WcsTrayScanStandardServer _wcsTrayScanStandardServer;
        private readonly IServiceScopeFactory _scopeFactory;
        private int _started = 0;
        private int _transactionId = 0;
        private readonly SemaphoreSlim _jobLock = new(1, 1);

        public PlcModbusHealthService(
            ILogger<PlcModbusHealthService> logger,
            CacheService cacheService,
            IMediator mediator,
            WcsTrayScanStandardServer wcsTrayScanStandardServer,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _cacheService = cacheService;
            _mediator = mediator;
            _wcsTrayScanStandardServer = wcsTrayScanStandardServer;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// 开始监控
        /// </summary>
        /// <param name="onStatusChanged"></param>
        public void StartMonitoring(Action<bool> onStatusChanged)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                return;
            }

            _ = Task.Run(() => MonitorLoopAsync(onStatusChanged, _cacheService.Token));
        }

        /// <summary>
        /// 监控循环
        /// </summary>
        /// <param name="onStatusChanged"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task MonitorLoopAsync(Action<bool> onStatusChanged, CancellationToken token)
        {
            bool? lastStatus = null;
            bool lastSignalHigh = false;
            while (!token.IsCancellationRequested)
            {
                var ip = MainStorage.Saves.PlcIp;
                bool isConnected = false;

                try
                {
                    if (string.IsNullOrWhiteSpace(ip))
                    {
                        if (lastStatus != false)
                        {
                            lastStatus = false;
                            onStatusChanged?.Invoke(false);
                        }

                        lastSignalHigh = false;
                        await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                        continue;
                    }

                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(ip, MainStorage.Saves.ModbusTcpPort);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2), token);
                    var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                    isConnected = completed == connectTask && client.Connected;

                    if (lastStatus != isConnected)
                    {
                        lastStatus = isConnected;
                        onStatusChanged?.Invoke(isConnected);
                        // _logger.LogInformation("Modbus TCP状态变更: {Status}, IP: {Ip}, Port: {Port}",
                        // isConnected ? "Connected" : "Disconnected",
                        // string.IsNullOrWhiteSpace(ip) ? "<empty>" : ip,
                        // MainStorage.Saves.ModbusTcpPort);

                        if (isConnected)
                        {
                            _logger.LogInformation("物流线Modbus连接成功: IP={IP}, Port={Port}, 触发寄存器=mw{TriggerAddr}, 结果寄存器=mw{ResultAddr}",
                                ip, MainStorage.Saves.ModbusTcpPort,
                                MainStorage.Saves.ModbusTriggerRegisterAddress,
                                MainStorage.Saves.ModbusResultRegisterAddress);
                        }
                    }

                    if (!isConnected)
                    {
                        lastSignalHigh = false;
                        await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                        continue;
                    }

                    using var stream = client.GetStream();

                    // 读寄存器
                    ushort signal = await ReadHoldingRegisterAsync(
                        stream,
                        MainStorage.Saves.ModbusTriggerRegisterAddress,
                        token).ConfigureAwait(false);

                    bool isHigh = signal == MainStorage.Saves.ModbusTriggerValue;

                    // 上升沿触发一次
                    if (isHigh && !lastSignalHigh)
                    {
                        _ = Task.Run(() => ExecuteCaptureAndUploadAsync(ip, token), token);
                    }
                    lastSignalHigh = isHigh;

                    await Task.Delay(TimeSpan.FromMilliseconds(200), token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (lastStatus != false)
                    {
                        lastStatus = false;
                        onStatusChanged?.Invoke(false);
                    }
                    lastSignalHigh = false;
                    _logger.LogWarning(ex, "Modbus轮询异常");
                    await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 执行捕获并上传同步数据
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ExecuteCaptureAndUploadAsync(string ip, CancellationToken token)
        {
            // 同一时刻只允许处理一个托盘，避免物流线重复触发时并发拍照、并发回写结果。
            if (!await _jobLock.WaitAsync(0, token).ConfigureAwait(false))
            {
                _logger.LogWarning("拍照任务正在执行，忽略重复触发");
                return;
            }

            try
            {
                // 新任务开始前先把 mw301 清零，避免物流线误读上一盘残留的 OK/NG 结果。
                await WriteResultRegisterAsync(ip, MainStorage.Saves.ModbusResultResetValue, token).ConfigureAwait(false);

                // 扫码依赖当前电池类型里的通道数、ROI 等配置；未选择时提示用户处理，处理后继续当前托盘任务。
                if (!await EnsureBatterySelectedAsync(token).ConfigureAwait(false))
                {
                    _logger.LogWarning("用户取消选择电池类型，本次任务结束");
                    return;
                }

                // 期望识别数量来自当前电池配置；重扫次数来自系统设置。
                int expectedCount = Math.Max(MainStorage.SelectBattery!.Count, 0);
                int retryTimes = Math.Clamp(MainStorage.Saves.DecodeRetryTimes, 0, 10);
                // 方案2：
                // 1. 首轮完整拍照、完整识别；
                // 2. 但二扫识别阶段只处理缺失通道对应的 ROI；
                // 3. 两轮结果最终按通道整合后上传 WCS。
                var decodedByChannel = new Dictionary<int, string>();
                // missingChannels 只跟踪“还没有拿到有效条码”的通道，用它决定二扫要补哪些位置。
                var missingChannels = expectedCount > 0
                    ? new System.Collections.Generic.HashSet<int>(Enumerable.Range(1, expectedCount))
                    : new System.Collections.Generic.HashSet<int>();

                // 首扫 + 补扫：只要仍有缺失通道，就按配置继续下一轮。
                for (int attempt = 0; attempt <= retryTimes; attempt++)
                {
                    // 第一轮 null 表示“全量识别”。
                    // 从第二轮开始只把缺失通道下发给识别层，减少无效 ROI 运算。
                    int[]? targetChannels = attempt == 0 || missingChannels.Count == 0
                        ? null
                        : missingChannels.OrderBy(channel => channel).ToArray();

                    if (targetChannels != null)
                    {
                        _logger.LogInformation(
                            // 注意：这里仍然会完整拍 6 张图，只是算法阶段只识别这些缺失通道。
                            "第{Attempt}次补扫开始，本轮仍会拍6张图，但仅识别缺失通道: {Channels}",
                            attempt + 1,
                            string.Join(",", targetChannels));
                    }

                    // DetectCCDCommand 会串起拍照、算法识别、结果汇总等完整扫码流程。
                    var detectResult = await _mediator.Send(
                        new DetectCCDCommand(MainStorage.SelectBattery),
                        token).ConfigureAwait(false);
                    detectResult.Match(
                        Right: right =>
                        {
                            foreach (var channel in right.Channels)
                            {
                                if (channel.Index <= 0)
                                {
                                    continue;
                                }

                                if (expectedCount > 0 && channel.Index > expectedCount)
                                {
                                    continue;
                                }

                                // 两轮结果按通道整合：
                                // 1. 只要本轮识别出了有效码，就写入最终结果；
                                // 2. 二扫不会用空结果/null 去覆盖首轮已识别出的有效码；
                                // 3. 一旦某通道拿到有效码，就从缺失集合里移除。
                                if (string.IsNullOrWhiteSpace(channel.Code) || channel.Code == "noread")
                                {
                                    continue;
                                }

                                decodedByChannel[channel.Index] = channel.Code;
                                missingChannels.Remove(channel.Index);
                            }
                            return 0;
                        },
                        Left: left =>
                        {
                            _logger.LogError($"第{attempt + 1}次拍照解码失败: {left}");
                            return 0;
                        });

                    // 当前缺失通道已经补齐后即可结束，不再继续补扫。
                    int decodedCount = expectedCount > 0
                        ? expectedCount - missingChannels.Count
                        : decodedByChannel.Count;
                    if (expectedCount > 0 && missingChannels.Count == 0)
                    {
                        _logger.LogInformation($"第{attempt + 1}次拍照已达到目标数量: {decodedCount}/{expectedCount}");
                        break;
                    }

                    if (attempt < retryTimes)
                    {
                        _logger.LogWarning($"第{attempt + 1}次拍照后解码数量不足: {decodedCount}/{expectedCount}，准备重扫");
                    }
                }

                // 保存扫码日志到数据库，供 PalletLogView 查看
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<LinxContext>();
                    var batteryInfo = Enumerable.Range(1, expectedCount)
                        .Select(ch => new BatteryInfo
                        {
                            BatteryCode = decodedByChannel.TryGetValue(ch, out var code) ? NormalizeCellCode(code) : "noread",
                            BatteryLevel = decodedByChannel.TryGetValue(ch, out var code2) && !string.IsNullOrWhiteSpace(code2) && code2 != "noread"
                                ? BatteryLevel.OK
                                : BatteryLevel.EMPTY
                        }).ToList();

                    var palletLog = new PalletLog
                    {
                        Column = MainStorage.SelectBattery?.Column ?? 0,
                        PalletType = PalletType.组盘,
                        ChannelCount = expectedCount,
                        BatteryInfo = batteryInfo,
                        ZuPanTime = DateTime.Now
                    };
                    db.PalletLogs.Add(palletLog);
                    await db.SaveChangesAsync(token).ConfigureAwait(false);

                    WeakReferenceMessenger.Default.Send(new PalletLogCreatedMessage(palletLog));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "保存扫码日志失败");
                }

                // 将识别结果按通道整理成逗号分隔字符串，未识别通道输出 "noread"。
                string payload = BuildCellCodePayload(decodedByChannel, expectedCount);
                _logger.LogInformation("本次任务给WCS传输条码【{Payload}】", payload);
                // 这里只判断“项目是否已成功把结果送入 WCS Socket 通道”。
                bool sendSuccess = _wcsTrayScanStandardServer.SendResult(payload);
                // 只有识别到全部条码才传 OK (1)，缺少任何一个条码都传 NG (2)。
                bool hasAllValidCodes = expectedCount > 0 && missingChannels.Count == 0;
                ushort resultValue = (sendSuccess && hasAllValidCodes)
                    ? MainStorage.Saves.ModbusResultOkValue
                    : MainStorage.Saves.ModbusResultNgValue;

                // 向物流线从站回写本次整盘扫码处理结果。
                await WriteResultRegisterAsync(ip, resultValue, token).ConfigureAwait(false);

                _logger.LogInformation(
                    "Modbus触发任务完成，mw300={TriggerValue} 已触发，mw301={ResultValue} 已反馈，回传条码数量: {Count}，目标数量: {ExpectedCount}，重扫次数配置: {RetryTimes}",
                    MainStorage.Saves.ModbusTriggerValue,
                    resultValue,
                    decodedByChannel.Count,
                    expectedCount,
                    retryTimes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行拍照解码并回传WCS时异常");
                try
                {
                    // 只要主流程任一环节异常，都尽量回写 NG，避免物流线一直等待结果。
                    await WriteResultRegisterAsync(ip, MainStorage.Saves.ModbusResultNgValue, token).ConfigureAwait(false);
                }
                catch (Exception writeEx)
                {
                    _logger.LogError(writeEx, "异常后回写 mw301 失败");
                }
            }
            finally
            {
                // 无论成功失败都要释放锁，保证下一次托盘到位还能继续执行。
                _jobLock.Release();
            }
        }

        private async Task<bool> EnsureBatterySelectedAsync(CancellationToken token)
        {
            if (MainStorage.SelectBattery is not null)
            {
                return true;
            }

            var result = await _mediator.Send(
                new WarningBoxCommand(
                    "当前未选择电池类型，请先在界面选择电池类型；选择完成后系统会自动重新拍照。\n点击取消则结束本次任务，且不回写NG。",
                    "电池类型未选择",
                    MessageBoxButton.OKCancel,
                    SaveLog: false),
                token).ConfigureAwait(false);

            if (result != MessageBoxResult.OK)
            {
                return false;
            }

            _logger.LogInformation("等待用户选择电池类型后继续当前扫码任务");

            while (!token.IsCancellationRequested)
            {
                if (MainStorage.SelectBattery is not null)
                {
                    _logger.LogInformation("检测到用户已选择电池类型：Id={Id}, Name={Name}",
                        MainStorage.SelectBattery.Id,
                        MainStorage.SelectBattery.TypeName);
                    return true;
                }

                await Task.Delay(500, token).ConfigureAwait(false);
            }

            return false;
        }

        
        private static string BuildCellCodePayload(Dictionary<int, string> decodedByChannel, int expectedCount)
        {
            if (expectedCount > 0)
            {
                var fields = Enumerable.Range(1, expectedCount)
                    .Select(ch => decodedByChannel.TryGetValue(ch, out var code)
                        ? NormalizeCellCode(code)
                        : "noread");
                return string.Join(",", fields);
            }

            var fields2 = decodedByChannel
                .OrderBy(kv => kv.Key)
                .Select(kv => NormalizeCellCode(kv.Value));
            return string.Join(",", fields2);
        }

        private static string NormalizeCellCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "noread";
            }

            return code;
        }

        
        private async Task<ushort> ReadHoldingRegisterAsync(NetworkStream stream, ushort address, CancellationToken token)
        {
            ushort tx = (ushort)Interlocked.Increment(ref _transactionId);
            byte unitId = MainStorage.Saves.ModbusUnitId;
            byte[] req =
            {
                (byte)(tx >> 8), (byte)tx,
                0x00, 0x00,
                0x00, 0x06,
                unitId,
                0x03,               // 读保持寄存器
                (byte)(address >> 8), (byte)address,
                0x00, 0x01
            };

            await stream.WriteAsync(req, 0, req.Length, token).ConfigureAwait(false);

            var header = new byte[7];
            await ReadExactAsync(stream, header, token).ConfigureAwait(false);

            int pduLen = (header[4] << 8) + header[5] - 1;
            if (pduLen <= 0)
            {
                throw new IOException("Modbus响应长度非法");
            }

            var pdu = new byte[pduLen];
            await ReadExactAsync(stream, pdu, token).ConfigureAwait(false);

            if (pdu[0] != 0x03 || pdu[1] != 0x02)
            {
                throw new IOException($"Modbus响应异常: Function={pdu[0]}, ByteCount={pdu[1]}");
            }

            return (ushort)((pdu[2] << 8) | pdu[3]);
        }

        private async Task WriteResultRegisterAsync(string ip, ushort value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new IOException("Modbus TCP未配置物流线IP");
            }

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, MainStorage.Saves.ModbusTcpPort);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2), token);
            var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
            if (completed != connectTask || !client.Connected)
            {
                throw new IOException($"连接物流线从站失败: {ip}:{MainStorage.Saves.ModbusTcpPort}");
            }

            using var stream = client.GetStream();
            await WriteSingleRegisterAsync(stream, MainStorage.Saves.ModbusResultRegisterAddress, value, token).ConfigureAwait(false);
        }

        private async Task WriteSingleRegisterAsync(NetworkStream stream, ushort address, ushort value, CancellationToken token)
        {
            ushort tx = (ushort)Interlocked.Increment(ref _transactionId);
            byte unitId = MainStorage.Saves.ModbusUnitId;
            byte[] req =
            {
                (byte)(tx >> 8), (byte)tx,
                0x00, 0x00,
                0x00, 0x06,
                unitId,
                0x06,
                (byte)(address >> 8), (byte)address,
                (byte)(value >> 8), (byte)value
            };

            await stream.WriteAsync(req, 0, req.Length, token).ConfigureAwait(false);

            var response = new byte[12];
            await ReadExactAsync(stream, response, token).ConfigureAwait(false);

            if (response[7] != 0x06)
            {
                throw new IOException($"Modbus写单寄存器响应异常: Function={response[7]}");
            }

            ushort responseAddress = (ushort)((response[8] << 8) | response[9]);
            ushort responseValue = (ushort)((response[10] << 8) | response[11]);
            if (responseAddress != address || responseValue != value)
            {
                throw new IOException($"Modbus写单寄存器回显异常: Address={responseAddress}, Value={responseValue}");
            }
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token).ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new IOException("Modbus连接已断开");
                }

                offset += read;
            }
        }
    }
}
