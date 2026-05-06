using LinxUniverse.SciSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Models;
using TrayScanStandard.Service.Models;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.Service
{
    /// <summary>
    /// WCS服务
    /// </summary>
    public class WcsTrayScanStandardServer
    {
        /// <summary>
        /// 使用Port端口，用于发送心跳
        /// </summary>
        private readonly LinxClientSocket _clientHeartSocket;

        /// <summary>
        /// 使用Port1端口，用于发送结果数据
        /// </summary>
        private readonly LinxClientSocket _clientDataSocket;

        private readonly MainViewModel _mainViewModel;
        private readonly ILogger<WcsTrayScanStandardServer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly object _statusLock = new();
        private bool _heartSocketConnected;
        private bool _dataSocketConnected; 

        public WcsTrayScanStandardServer(
            ILogger<WcsTrayScanStandardServer> logger,
            MainViewModel mainViewModel,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _mainViewModel = mainViewModel;
            _scopeFactory = scopeFactory;

            var heartPort = int.TryParse(MainStorage.Saves.WcsHeartPort.ToString(), out var hp) ? hp : 10001;
            var dataPort = int.TryParse(MainStorage.Saves.WcsDataPort.ToString(), out var dp) ? dp : 10002;

            _clientHeartSocket = new LinxClientSocket(new LinxClientSocketOption
            {
                Logger = logger,
                Address = MainStorage.Saves.WcsIP,
                Port = heartPort,
                SplitChar = '\n',
            });
            _clientDataSocket = new LinxClientSocket(new LinxClientSocketOption
            {
                Logger = logger,
                Address = MainStorage.Saves.WcsIP,
                Port = dataPort,
                SplitChar = '\n',
            });

            Init();
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        public void SendHeartbeat(HeartbeatsRequest data)
        {
            if (!HasValidWcsIp())
            {
                return;
            }
            //_clientSocket.Send("HEARTBEAT");
            _clientHeartSocket.Send(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        }

        /// <summary>
        /// 发送结果
        /// </summary>
        public void SendResult(CellCodeDateRequest cellCodeDateRequest)
        {
            if (!HasValidWcsIp())
            {
                return;
            }
            string data = JsonSerializer.Serialize(cellCodeDateRequest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            _clientDataSocket.Send(Encoding.UTF8.GetBytes(data));

            // 原实现（保留）
            // _linxContext.WcsLogs.Add(new WcsLog
            // {
            //     Request = data,
            // });
            // _linxContext.SaveChanges();

            SaveWcsLog(data, isSuccess: true, apiName: "socket/sendResult", response: string.Empty);
        }

        public void Init()
        {
            if (!HasValidWcsIp())
            {
                _logger.LogWarning("WCS IP为空，跳过Socket连接初始化。配置后重启程序生效。");
                _mainViewModel.WcsIsRunning = false;
                return;
            }

            _clientHeartSocket.OnComplete += ClientHeartSocket_OnComplete;
            _clientHeartSocket.RecviceLine += ClientHeartSocket_RecviceLine;
            _clientHeartSocket.OnConnect += ClientHeartSocket_OnConnect;
            _clientHeartSocket.Connect();

            _clientDataSocket.OnComplete += _clientDataSocket_OnComplete;
            _clientDataSocket.RecviceLine += _clientDataSocket_RecviceLine;
            _clientDataSocket.OnConnect += _clientDataSocket_OnConnect;
            _clientDataSocket.Connect();
        }

        /// <summary>
        /// 当辅助Socket连接成功时触发
        /// </summary>
        private void _clientDataSocket_OnConnect()
        {
            // 原实现（保留）
            // throw new NotImplementedException();
            lock (_statusLock)
            {
                _dataSocketConnected = true;
                _mainViewModel.WcsIsRunning = _heartSocketConnected && _dataSocketConnected;
            }
            _logger.LogInformation("WCS数据Socket连接成功");
        }

        /// <summary>
        /// 当辅助Socket接收到数据行时触发
        /// </summary>
        private void _clientDataSocket_RecviceLine(string obj)
        {
            // 原实现（保留）
            // throw new NotImplementedException();
            _logger.LogInformation($"WCS数据Socket收到: {obj}");
        }

        /// <summary>
        /// 当辅助Socket连接断开时触发，会：自动重新连接 _clientDataSocket.Connect()
        /// </summary>
        private void _clientDataSocket_OnComplete()
        {
            // 原实现（保留）
            // throw new NotImplementedException();
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
            _clientDataSocket.Connect();
        }

        /// <summary>
        /// 当主Socket连接成功时触发
        /// </summary>
        private void ClientHeartSocket_OnConnect()
        {
            lock (_statusLock)
            {
                _heartSocketConnected = true;
                _mainViewModel.WcsIsRunning = _heartSocketConnected && _dataSocketConnected;
            }
            _logger.LogInformation("WCS主Socket连接成功");
        }

        /// <summary>
        /// 当主Socket连接断开时触发
        /// </summary>
        private void ClientHeartSocket_OnComplete()
        {
            // 原实现（保留）
            // throw new NotImplementedException();
            lock (_statusLock)
            {
                _heartSocketConnected = false;
                _mainViewModel.WcsIsRunning = false;
            }
            _logger.LogWarning("WCS主Socket断开，准备重连");
            if (!HasValidWcsIp())
            {
                return;
            }
            _clientHeartSocket.Connect();
        }

        /// <summary>
        /// 当主Socket接收到数据行时触发
        /// </summary>
        private void ClientHeartSocket_RecviceLine(string obj)
        {
            _logger.LogInformation($"WCS主Socket收到: {obj}");
        }

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

    }
}
