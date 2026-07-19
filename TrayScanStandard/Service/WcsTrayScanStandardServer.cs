using LinxUniverse.SciSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using MediatR;
using System.Threading.Tasks;
using System.Linq;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Models;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.Service
{
    /// <summary>
    /// WCS服务
    /// </summary>
    public class WcsTrayScanStandardServer
    {
        // 【已移除心跳】
        // /// <summary>
        // /// 使用Port端口，用于发送心跳
        // /// </summary>
        // private LinxClientSocket? _clientHeartSocket;

        /// <summary>
        /// 使用Port1端口，用于发送结果数据
        /// </summary>
        private LinxClientSocket? _clientDataSocket;

        private readonly MainViewModel _mainViewModel;
        private readonly ILogger<WcsTrayScanStandardServer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly object _socketLock = new();
        private readonly object _statusLock = new();
        // 【已移除心跳】private bool _heartSocketConnected;
        private bool _dataSocketConnected; 
        private int _reconnectAttempt;

        public WcsTrayScanStandardServer(
            ILogger<WcsTrayScanStandardServer> logger,
            MainViewModel mainViewModel,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _mainViewModel = mainViewModel;
            _scopeFactory = scopeFactory;

            ReloadSettings();
        }

        // 【已移除心跳】
        // /// <summary>
        // /// 发送心跳
        // /// </summary>
        // public void SendHeartbeat(HeartbeatsRequest data)
        // {
        //     if (!HasValidWcsIp())
        //     {
        //         return;
        //     }
        //
        //     LinxClientSocket? socket;
        //     lock (_socketLock)
        //     {
        //         socket = _clientHeartSocket;
        //     }
        //
        //     socket?.Send(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        // }

        /// <summary>
        /// 发送结果
        /// </summary>
        public bool SendResult(string payload)
        {
            bool result;
            if (!HasValidWcsIp())
            {
                _logger.LogWarning("WCS IP为空，无法发送扫码结果");
                return false;
            }

            LinxClientSocket? socket;
            lock (_socketLock)
            {
                socket = _clientDataSocket;
            }

            if (socket == null)
            {
                _logger.LogWarning("WCS数据Socket未初始化，无法发送扫码结果");
                return false;
            }

            try
            {
                socket.Send(Encoding.UTF8.GetBytes(payload));
                result = true;
            }
            catch (Exception ex)
            {
                SaveWcsLog(payload, isSuccess: false, apiName: "socket/sendResult", response: ex.Message);
                _logger.LogError(ex, "WCS扫码结果发送失败");
                result = false;
            }
            if (result)
                SaveWcsLog(payload, isSuccess: true, apiName: "socket/sendResult", response: string.Empty);
            _logger.LogInformation("发送结果 {Result}", result);
            // 旧格式(仅参考): _logger.LogInformation($"发送结果 {result}, \"{payload}\"");
            return result;
        }

        public void ReloadSettings()
        {
            // 【已移除心跳】LinxClientSocket? oldHeartSocket;
            LinxClientSocket? oldDataSocket;

            lock (_socketLock)
            {
                // 【已移除心跳】oldHeartSocket = _clientHeartSocket;
                oldDataSocket = _clientDataSocket;
                // 【已移除心跳】_clientHeartSocket = null;
                _clientDataSocket = null;

                ResetSocketStatus();
            }

            // 【已移除心跳】
            // try
            // {
            //     oldHeartSocket?.Disconnect();
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogWarning(ex, "关闭旧WCS心跳Socket时发生异常");
            // }

            try
            {
                oldDataSocket?.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭旧WCS数据Socket时发生异常");
            }

            if (!HasValidWcsIp())
            {
                _logger.LogWarning("WCS IP为空，跳过Socket连接初始化。");
                return;
            }

            // 【已移除心跳】var heartSocket = CreateSocket(GetHeartPort());
            var dataSocket = CreateSocket(GetDataPort());

            // 【已移除心跳】AttachHeartSocketHandlers(heartSocket);
            AttachDataSocketHandlers(dataSocket);

            lock (_socketLock)
            {
                // 【已移除心跳】_clientHeartSocket = heartSocket;
                _clientDataSocket = dataSocket;
            }

            // 【已移除心跳】heartSocket.Connect();
            dataSocket.Connect();
        }

   private LinxClientSocket CreateSocket(ushort port)
   {
        return new LinxClientSocket(new LinxClientSocketOption
        {
            Address = MainStorage.Saves.WcsIP,
            Port = port,
            SplitChar = default,
        })
        {
            Logger = _logger
        };
   }

        // 【已移除心跳】
        // private void AttachHeartSocketHandlers(LinxClientSocket socket)
        // {
        //     socket.OnConnect += () => ClientHeartSocket_OnConnect(socket);
        //     socket.RecviceLine += message => ClientHeartSocket_RecviceLine(socket, message);
        //     socket.OnComplete += () => ClientHeartSocket_OnComplete(socket);
        // }

        private void AttachDataSocketHandlers(LinxClientSocket socket)
        {
            socket.OnConnect += () => ClientDataSocket_OnConnect(socket);
            socket.RecviceLine += message => ClientDataSocket_RecviceLine(socket, message);
            socket.OnComplete += () => ClientDataSocket_OnComplete(socket);
        }

        /// <summary>
        /// 当辅助Socket连接成功时触发
        /// </summary>
        private void ClientDataSocket_OnConnect(LinxClientSocket socket)
        {
            if (!ReferenceEquals(_clientDataSocket, socket))
            {
                return;
            }

            lock (_statusLock)
            {
                _dataSocketConnected = true;
                _mainViewModel.WcsIsRunning = _dataSocketConnected;
            }
            _logger.LogInformation("WCS连接成功: IP={IP}, Port={Port}", socket.Option.Address, socket.Option.Port);
        }

        /// <summary>
        /// 当辅助Socket接收到数据行时触发
        /// </summary>
        private void ClientDataSocket_RecviceLine(LinxClientSocket socket, string obj)
        {
            if (!ReferenceEquals(_clientDataSocket, socket))
            {
                return;
            }

            _logger.LogInformation($"WCS数据Socket收到: {obj}");

            // 当收到WCS发送 "+" 时触发扫码拍照
            if (obj.Trim() == "+")
            {
                _logger.LogInformation("WCS触发扫码拍照");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                        var db = scope.ServiceProvider.GetRequiredService<TrayScanStandard.Data.LinxContext>();

                        // 获取电池型号信息（优先使用缓存，否则从数据库加载）
                        var battery = MainStorage.SelectBattery;
                        if (battery == null)
                        {
                            battery = db.BatteryTypeInfos
                                .FirstOrDefault(s => s.Id == MainStorage.Saves.SelectBatteryId)
                                ?? db.BatteryTypeInfos.FirstOrDefault();

                            if (battery != null)
                            {
                                MainStorage.SelectBattery = battery;
                            }
                        }

                        if (battery == null)
                        {
                            _logger.LogWarning("WCS触发扫码拍照失败：未找到电池型号信息");
                            return;
                        }

                        _logger.LogInformation("WCS触发扫码拍照：电池={BatteryType}", battery.TypeName);

                        var result = await mediator.Send(new TrayScanStandard.Mediator.Commands.DetectCCDCommand(battery));

                        result.Match(
                            Right: r =>
                            {
                                try
                                {
                                    _logger.LogInformation("WCS触发扫码拍照成功，通道数={Count}", r.Channels.Length);

                                    // 按电池通道序号填充扫码结果（未识别到的通道设为空字符串）
                                    var batteryCodes = Enumerable.Range(1, battery.Count)
                                        .Select(i => r.Channels
                                            .FirstOrDefault(c => c.Index == i)?.Code ?? "")
                                        .ToList();

                                    // 以逗号分隔的格式拼接扫码结果，未识别通道显示 noread
                                    var successPayload = string.Join(",", batteryCodes.Select(s => string.IsNullOrEmpty(s) ? "noread" : s));
                                    SendResult(successPayload);

                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "发送WCS扫码结果失败");
                                }
                            },
                            Left: err =>
                            {
                                _logger.LogError("WCS触发扫码拍照失败：{Error}", err);
                                try
                                {
                                    // 所有通道标记为 noread
                                    var errorPayload = string.Join(",", Enumerable.Range(1, battery.Count).Select(_ => "noread"));

                                    SendResult(errorPayload);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "发送WCS扫码错误结果失败");
                                }
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WCS触发扫码拍照异常");
                    }
                });
            }
        }
        /// <summary>
        /// 当辅助Socket连接断开时触发，自动延迟重连（1s → 3s → 5s 循环）
        /// </summary>
        private async void ClientDataSocket_OnComplete(LinxClientSocket socket)
        {
            if (!ReferenceEquals(_clientDataSocket, socket))
            {
                return;
            }

            lock (_statusLock)
            {
                _dataSocketConnected = false;
                _mainViewModel.WcsIsRunning = false;
            }
            _logger.LogWarning("WCS数据Socket断开，准备重连");
            if (!HasValidWcsIp())
            {
                return;
            }

            // 延迟重连：第1次1秒，第2次3秒，第3次5秒，循环
            var delays = new[] { 1000, 3000, 5000 };
            var delay = delays[_reconnectAttempt];
            _reconnectAttempt = (_reconnectAttempt + 1) % 3;
            _logger.LogInformation("WCS数据Socket将在{Delay}ms后重连", delay);
            try
            {
                await Task.Delay(delay);
            }
            catch
            {
                return;
            }

            socket.Connect();
        }

        // 【已移除心跳】
        // /// <summary>
        // /// 当主Socket连接成功时触发
        // /// </summary>
        // private void ClientHeartSocket_OnConnect(LinxClientSocket socket)
        // {
        //     if (!ReferenceEquals(_clientHeartSocket, socket))
        //     {
        //         return;
        //     }
        //
        //     lock (_statusLock)
        //     {
        //         _heartSocketConnected = true;
        //         _mainViewModel.WcsIsRunning = _heartSocketConnected && _dataSocketConnected;
        //     }
        //     _logger.LogInformation("WCS主Socket连接成功");
        // }

        // 【已移除心跳】
        // /// <summary>
        // /// 当主Socket连接断开时触发
        // /// </summary>
        // private void ClientHeartSocket_OnComplete(LinxClientSocket socket)
        // {
        //     if (!ReferenceEquals(_clientHeartSocket, socket))
        //     {
        //         return;
        //     }
        //
        //     lock (_statusLock)
        //     {
        //         _heartSocketConnected = false;
        //         _mainViewModel.WcsIsRunning = false;
        //     }
        //     _logger.LogWarning("WCS主Socket断开，准备重连");
        //     if (!HasValidWcsIp())
        //     {
        //         return;
        //     }
        //     socket.Connect();
        // }

        // 【已移除心跳】
        // /// <summary>
        // /// 当主Socket接收到数据行时触发
        // /// </summary>
        // private void ClientHeartSocket_RecviceLine(LinxClientSocket socket, string obj)
        // {
        //     if (!ReferenceEquals(_clientHeartSocket, socket))
        //     {
        //         return;
        //     }
        //
        //     _logger.LogInformation($"WCS主Socket收到: {obj}");
        // }

        /// <summary>
        /// 保存WCS日志
        /// </summary>
        /// <param name="request"></param>
        /// <param name="isSuccess"></param>
        /// <param name="apiName"></param>
        /// <param name="response"></param>
        private void SaveWcsLog(string request, bool isSuccess, string apiName, string? response)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LinxContext>();
                db.WcsLogs.Add(new WcsLog
                {
                    ApiName = apiName,
                    IsSuccess = isSuccess,
                    RequestTime = DateTime.Now,
                    ResponseTime = DateTime.Now,
                    Request = request,
                    Response = response ?? string.Empty
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入WCS Socket日志失败");
            }
        }

        private static bool HasValidWcsIp()
        {
            return !string.IsNullOrWhiteSpace(MainStorage.Saves.WcsIP);
        }

        // 【已移除心跳】
        // private static ushort GetHeartPort()
        // {
        //     return ushort.TryParse(MainStorage.Saves.WcsHeartPort.ToString(), out var port) ? port : (ushort)10001;
        // }

        private static ushort GetDataPort()
        {
            return ushort.TryParse(MainStorage.Saves.WcsDataPort.ToString(), out var port) ? port : (ushort)10002;
        }

        private void ResetSocketStatus()
        {
            lock (_statusLock)
            {
                // 【已移除心跳】_heartSocketConnected = false;
                _dataSocketConnected = false;
                _mainViewModel.WcsIsRunning = false;
            }
        }

    }
}
