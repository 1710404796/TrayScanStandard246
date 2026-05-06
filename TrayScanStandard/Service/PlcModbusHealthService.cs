using LinxUniverse.DI;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands;
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
        private int _started = 0;
        private int _transactionId = 0;
        private readonly SemaphoreSlim _jobLock = new(1, 1);

        // 触发寄存器配置（默认）
        private const ushort TriggerRegisterAddress = 0;
        private const ushort TriggerHighValue = 1;
        private const byte UnitId = 1;

        public PlcModbusHealthService(
            ILogger<PlcModbusHealthService> logger,
            CacheService cacheService,
            IMediator mediator,
            WcsTrayScanStandardServer wcsTrayScanStandardServer)
        {
            _logger = logger;
            _cacheService = cacheService;
            _mediator = mediator;
            _wcsTrayScanStandardServer = wcsTrayScanStandardServer;
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
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(ip, 502);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2), token);
                    var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                    isConnected = completed == connectTask && client.Connected;

                    if (lastStatus != isConnected)
                    {
                        lastStatus = isConnected;
                        onStatusChanged?.Invoke(isConnected);
                        _logger.LogInformation("Modbus TCP状态变更: {Status}, IP: {Ip}, Port: 502",
                            isConnected ? "Connected" : "Disconnected",
                            string.IsNullOrWhiteSpace(ip) ? "<empty>" : ip);
                    }

                    if (!isConnected)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                        continue;
                    }

                    using var stream = client.GetStream();
                    ushort signal = await ReadHoldingRegisterAsync(stream, TriggerRegisterAddress, token).ConfigureAwait(false);
                    bool isHigh = signal == TriggerHighValue;

                    // 上升沿触发一次
                    if (isHigh && !lastSignalHigh)
                    {
                        _ = Task.Run(() => ExecuteCaptureAndUploadAsync(token), token);
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
        private async Task ExecuteCaptureAndUploadAsync(CancellationToken token)
        {
            if (!await _jobLock.WaitAsync(0, token).ConfigureAwait(false))
            {
                _logger.LogWarning("拍照任务正在执行，忽略重复触发");
                return;
            }

            try
            {
                if (MainStorage.SelectBattery is null)
                {
                    _logger.LogWarning("未选择电池类型，无法执行拍照解码");
                    return;
                }
                int expectedCount = Math.Max(MainStorage.SelectBattery.Count, 0);
                int retryTimes = Math.Clamp(MainStorage.Saves.DecodeRetryTimes, 0, 10);
                var decodedByChannel = new Dictionary<int, string>();

                for (int attempt = 0; attempt <= retryTimes; attempt++)
                {
                    var detectResult = await _mediator.Send(new DetectCCDCommand(MainStorage.SelectBattery), token).ConfigureAwait(false);
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

                                decodedByChannel[channel.Index] = string.IsNullOrWhiteSpace(channel.Code) ? "null" : channel.Code;
                            }
                            return 0;
                        },
                        Left: left =>
                        {
                            _logger.LogError($"第{attempt + 1}次拍照解码失败: {left}");
                            return 0;
                        });

                    int decodedCount = decodedByChannel.Count;
                    if (expectedCount > 0 && decodedCount >= expectedCount)
                    {
                        _logger.LogInformation($"第{attempt + 1}次拍照已达到目标数量: {decodedCount}/{expectedCount}");
                        break;
                    }

                    if (attempt < retryTimes)
                    {
                        _logger.LogWarning($"第{attempt + 1}次拍照后解码数量不足: {decodedCount}/{expectedCount}，准备重扫");
                    }
                }

                var payload = BuildCellCodePayload(decodedByChannel, expectedCount);
                _wcsTrayScanStandardServer.SendResult(payload);
                _logger.LogInformation($"Modbus触发任务完成，回传条码数量: {payload.Data.Count}，目标数量: {expectedCount}，重扫次数配置: {retryTimes}");
            }
            catch
            {
                _logger.LogError("执行拍照解码并回传WCS时异常");
            }
            finally
            {
                _jobLock.Release();
            }
        }

        
        private static CellCodeDateRequest BuildCellCodePayload(Dictionary<int, string> decodedByChannel, int expectedCount)
        {
            var payload = new CellCodeDateRequest();
            var ccdCode = string.IsNullOrWhiteSpace(MainStorage.Saves.SystemCode) ? "null" :MainStorage.Saves.SystemCode ;
            if (expectedCount > 0)
            {
                payload.Data = Enumerable.Range(1, expectedCount)
                    .Select(channel => new CellCodeDateRequest.CellData
                    {
                        Channel = channel,
                        CellCode = decodedByChannel.TryGetValue(channel, out var code) ? NormalizeCellCode(code) : "null",
                        CcdCode = ccdCode
                    })
                    .ToList();
                return payload;
            }

            payload.Data = decodedByChannel
                .OrderBy(kv => kv.Key)
                .Select(kv => new CellCodeDateRequest.CellData
                {
                    Channel = kv.Key,
                    CellCode = NormalizeCellCode(kv.Value),
                    CcdCode = ccdCode
                })
                .ToList();
            return payload;
        }

        private static string NormalizeCellCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "null";
            }

            return code;
        }

        
        private async Task<ushort> ReadHoldingRegisterAsync(NetworkStream stream, ushort address, CancellationToken token)
        {
            ushort tx = (ushort)Interlocked.Increment(ref _transactionId);
            byte[] req =
            {
                (byte)(tx >> 8), (byte)tx,
                0x00, 0x00,
                0x00, 0x06,
                UnitId,
                0x03,
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
