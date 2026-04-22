using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialCommunicate;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrayScanStandard.Models;
using TrayScanStandard.Services;
using TrayScanStandard.ViewModels;


namespace TrayScanStandard.ViewModel
{
    public partial class LightSourceControlViewModel : ObservableRecipient
    {
        private ILightService _currentLightService;
        private readonly WordopLightService _wordopService;
        private readonly CognexLightService _cognexService;
        private bool _isConnecting;
        private bool _isDisconnecting;

        // 防抖定时器
        private DispatcherTimer _debounceTimer;
        private int _pendingChannel;
        private int _pendingBrightness;

        // 配置文件路径
        private string _configDirectory;
        private string _globalConfigPath;
        private string _wordopConfigPath;
        private string _cognexConfigPath;

        public LightSourceControlViewModel(WordopLightService wordopService, CognexLightService cognexService)
        {
            _wordopService = wordopService;
            _cognexService = cognexService;

            InitializeConfigPaths();
            InitializeCommands();
            LoadComPorts();
            InitDebounceTimer();
            LoadGlobalConfig();
            InitializeLightService();
            LoadCurrentLightConfig();
        }

        #region 配置文件路径初始化

        private void InitializeConfigPaths()
        {
            _configDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!System.IO.Directory.Exists(_configDirectory))
            {
                System.IO.Directory.CreateDirectory(_configDirectory);
            }

            _globalConfigPath = System.IO.Path.Combine(_configDirectory, "GlobalConfig.json");
            _wordopConfigPath = System.IO.Path.Combine(_configDirectory, "WordopConfig.json");
            _cognexConfigPath = System.IO.Path.Combine(_configDirectory, "CognexConfig.json");
        }

        #endregion

        #region 日志方法
        private string _statusMessage = "就绪";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private void Log(string message, bool showInStatusBar = false)
        {
            string cleanMessage = message?.Replace("\n", " ").Replace("\r", " ") ?? "";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {cleanMessage}");
            if (showInStatusBar)
            {
                StatusMessage = cleanMessage;
            }
        }

        private void LogInfo(string message, bool showInStatusBar = false)
        {
            Serilog.Log.Information("[Light] {Message}", message);
            Log($"[INFO] {message}", showInStatusBar);
        }

        private void LogWarning(string message, bool showInStatusBar = true)
        {
            Serilog.Log.Warning("[Light] {Message}", message);
            Log($"[WARN] {message}", showInStatusBar);
        }

        private void LogError(string message, bool showInStatusBar = true)
        {
            Serilog.Log.Error("[Light] {Message}", message);
            Log($"[ERROR] {message}", showInStatusBar);
        }

        private void LogSuccess(string message, bool showInStatusBar = true)
        {
            Serilog.Log.Information("[Light] {Message}", message);
            Log($"[SUCCESS] {message}", showInStatusBar);
        }

        #endregion



        #region 初始化方法

        private void InitDebounceTimer()
        {
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(100);
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            await SetChannelBrightnessImmediate(_pendingChannel, _pendingBrightness);
        }

        private void ScheduleSetBrightness(int channel, int brightness)
        {
            _pendingChannel = channel;
            _pendingBrightness = brightness;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async Task SetChannelBrightnessImmediate(int channel, int brightness)
        {
            if (!IsConnected || _currentLightService == null)
                return;

            try
            {
                await _currentLightService.SetBrightnessAsync(channel, brightness);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"实时控制失败: CH{channel} - {ex.Message}");
            }
        }

        private void InitializeCommands()
        {
            ConnectCommand = new RelayLightCommand(async _ => await ConnectAsync(), _ => CanConnect());
            DisconnectCommand = new RelayLightCommand(async _ => await DisconnectAsync(), _ => CanDisconnect());
            RefreshComPortsCommand = new RelayLightCommand(_ => LoadComPorts());
            TurnOnAllCommand = new RelayLightCommand(async _ => await SetAllChannelsAsync(255), _ => CanOperate());
            TurnOffAllCommand = new RelayLightCommand(async _ => await SetAllChannelsAsync(0), _ => CanOperate());
            SaveConfigCommand = new RelayLightCommand(_ => SaveCurrentLightConfig());
            ReadAllChannelsCommand = new RelayLightCommand(async _ => await ReadAllChannelsAsync(), _ => CanOperate());
            ReadVersionCommand = new RelayLightCommand(async _ => await ReadVersionAsync(), _ => CanOperate());
        }

        private void InitializeLightService()
        {
            switch (SelectedLightType)
            {
                case LightType.Wordop:
                    _currentLightService = _wordopService;
                    CurrentLightServiceName = "Wordop";
                    break;
                case LightType.Cognex:
                    _currentLightService = _cognexService;
                    CurrentLightServiceName = "Cognex";
                    break;
            }

            IsConnected = _currentLightService?.IsConnected ?? false;
        }

        #endregion

        #region 属性

        private ObservableCollection<string> _comPorts;
        public ObservableCollection<string> ComPorts
        {
            get => _comPorts;
            set { _comPorts = value; OnPropertyChanged(); }
        }

        private string _selectedComPort;
        public string SelectedComPort
        {
            get => _selectedComPort;
            set
            {
                if (_selectedComPort != value)
                {
                    _selectedComPort = value;
                    OnPropertyChanged();
                    RefreshCommands();
                }
            }
        }

        private ObservableCollection<int> _baudRates;
        public ObservableCollection<int> BaudRates
        {
            get => _baudRates;
            set { _baudRates = value; OnPropertyChanged(); }
        }

        private int _selectedBaudRate = 19200;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set { _selectedBaudRate = value; OnPropertyChanged(); }
        }

        private LightType _selectedLightType = LightType.Wordop;
        public LightType SelectedLightType
        {
            get => _selectedLightType;
            set
            {
                if (_selectedLightType != value)
                {
                    SaveCurrentLightConfig();
                    SaveGlobalConfig();
                    _selectedLightType = value;
                    OnPropertyChanged();
                    InitializeLightService();
                    LoadCurrentLightConfig();
                    OnPropertyChanged(nameof(CurrentLightServiceName));

                    if (IsConnected)
                    {
                        _ = DisconnectAsync();
                    }

                    // 刷新命令状态
                    RefreshCommands();
                }
            }
        }

        private ObservableCollection<LightType> _lightTypes;
        public ObservableCollection<LightType> LightTypes
        {
            get => _lightTypes;
            set { _lightTypes = value; OnPropertyChanged(); }
        }

        private int _channel1Brightness;
        public int Channel1Brightness
        {
            get => _channel1Brightness;
            set
            {
                int newValue = Math.Min(Math.Max(value, 0), 255);
                if (_channel1Brightness != newValue)
                {
                    _channel1Brightness = newValue;
                    OnPropertyChanged();

                    if (IsConnected)
                        ScheduleSetBrightness(1, newValue);

                    SaveCurrentLightConfig();
                }
            }
        }

        private int _channel2Brightness;
        public int Channel2Brightness
        {
            get => _channel2Brightness;
            set
            {
                int newValue = Math.Min(Math.Max(value, 0), 255);
                if (_channel2Brightness != newValue)
                {
                    _channel2Brightness = newValue;
                    OnPropertyChanged();

                    if (IsConnected)
                        ScheduleSetBrightness(2, newValue);

                    SaveCurrentLightConfig();
                }
            }
        }

        private int _channel3Brightness;
        public int Channel3Brightness
        {
            get => _channel3Brightness;
            set
            {
                int newValue = Math.Min(Math.Max(value, 0), 255);
                if (_channel3Brightness != newValue)
                {
                    _channel3Brightness = newValue;
                    OnPropertyChanged();

                    if (IsConnected)
                        ScheduleSetBrightness(3, newValue);

                    SaveCurrentLightConfig();
                }
            }
        }

        private int _channel4Brightness;
        public int Channel4Brightness
        {
            get => _channel4Brightness;
            set
            {
                int newValue = Math.Min(Math.Max(value, 0), 255);
                if (_channel4Brightness != newValue)
                {
                    _channel4Brightness = newValue;
                    OnPropertyChanged();

                    if (IsConnected)
                        ScheduleSetBrightness(4, newValue);

                    SaveCurrentLightConfig();
                }
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                RefreshCommands();
            }
        }

        private string _currentLightServiceName;
        public string CurrentLightServiceName
        {
            get => _currentLightServiceName;
            set { _currentLightServiceName = value; OnPropertyChanged(); }
        }

        private string _versionInfo;
        public string VersionInfo
        {
            get => _versionInfo;
            set { _versionInfo = value; OnPropertyChanged(); }
        }

        private string _readStatus;
        public string ReadStatus
        {
            get => _readStatus;
            set { _readStatus = value; OnPropertyChanged(); }
        }

        #endregion

        #region 命令

        public ICommand ConnectCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        public ICommand RefreshComPortsCommand { get; private set; }
        public ICommand TurnOnAllCommand { get; private set; }
        public ICommand TurnOffAllCommand { get; private set; }
        public ICommand SaveConfigCommand { get; private set; }
        public ICommand ReadAllChannelsCommand { get; private set; }
        public ICommand ReadVersionCommand { get; private set; }

        #endregion

        #region 命令状态判断

        private bool CanConnect() =>
            !IsConnected &&
            !_isConnecting &&
            !_isDisconnecting &&
            _currentLightService != null &&
            !string.IsNullOrEmpty(SelectedComPort);

        private bool CanDisconnect() =>
            IsConnected &&
            !_isConnecting &&
            !_isDisconnecting &&
            _currentLightService != null;

        private bool CanOperate() =>
            IsConnected &&
            !_isConnecting &&
            !_isDisconnecting &&
            _currentLightService != null;

        private void RefreshCommands()
        {
            (ConnectCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
            (DisconnectCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
            (TurnOnAllCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
            (TurnOffAllCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
            (ReadAllChannelsCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
            (ReadVersionCommand as RelayLightCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region 配置保存/加载

        [Serializable]
        private class LightConfig
        {
            public int Channel1Brightness { get; set; }
            public int Channel2Brightness { get; set; }
            public int Channel3Brightness { get; set; }
            public int Channel4Brightness { get; set; }
            public string LastComPort { get; set; }
            public int LastBaudRate { get; set; }
        }

        [Serializable]
        private class GlobalConfig
        {
            public LightType LastLightType { get; set; }
        }

        private string GetCurrentLightConfigPath()
        {
            switch (SelectedLightType)
            {
                case LightType.Wordop: return _wordopConfigPath;
                case LightType.Cognex: return _cognexConfigPath;
                default: return _wordopConfigPath;
            }
        }

        private void LoadGlobalConfig()
        {
            try
            {
                if (System.IO.File.Exists(_globalConfigPath))
                {
                    string json = System.IO.File.ReadAllText(_globalConfigPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<GlobalConfig>(json);
                    if (config != null)
                    {
                        _selectedLightType = config.LastLightType;
                        OnPropertyChanged(nameof(SelectedLightType));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载全局配置失败: {ex.Message}");
            }
        }

        private void SaveGlobalConfig()
        {
            try
            {
                var config = new GlobalConfig { LastLightType = SelectedLightType };
                string json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_globalConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存全局配置失败: {ex.Message}");
            }
        }

        private void SaveCurrentLightConfig()
        {
            try
            {
                var config = new LightConfig
                {
                    Channel1Brightness = Channel1Brightness,
                    Channel2Brightness = Channel2Brightness,
                    Channel3Brightness = Channel3Brightness,
                    Channel4Brightness = Channel4Brightness,
                    LastComPort = SelectedComPort ?? "",
                    LastBaudRate = SelectedBaudRate
                };

                string configPath = GetCurrentLightConfigPath();
                string directory = System.IO.Path.GetDirectoryName(configPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                string json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(configPath, json);
                string comDisplay = string.IsNullOrEmpty(SelectedComPort) ? "未选择" : SelectedComPort;
                LogInfo($"配置已自动保存 | {CurrentLightServiceName} | CH1={Channel1Brightness} CH2={Channel2Brightness} CH3={Channel3Brightness} CH4={Channel4Brightness} | COM={comDisplay} | 波特率={SelectedBaudRate}", showInStatusBar: false);
            }
            catch (Exception ex)
            {
                LogError($"保存配置失败: {ex.Message}");
            }
        }

        private void LoadCurrentLightConfig()
        {
            try
            {
                string configPath = GetCurrentLightConfigPath();

                if (System.IO.File.Exists(configPath))
                {
                    string json = System.IO.File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<LightConfig>(json);

                    if (config != null)
                    {
                        Channel1Brightness = Math.Min(config.Channel1Brightness, 255);
                        Channel2Brightness = Math.Min(config.Channel2Brightness, 255);
                        Channel3Brightness = Math.Min(config.Channel3Brightness, 255);
                        Channel4Brightness = Math.Min(config.Channel4Brightness, 255);

                        if (!string.IsNullOrEmpty(config.LastComPort))
                            SelectedComPort = config.LastComPort;

                        if (config.LastBaudRate > 0)
                            SelectedBaudRate = config.LastBaudRate;

                        string comDisplay = string.IsNullOrEmpty(SelectedComPort) ? "未选择" : SelectedComPort;
                        LogSuccess($"已加载配置 | {CurrentLightServiceName} | CH1={Channel1Brightness} CH2={Channel2Brightness} CH3={Channel3Brightness} CH4={Channel4Brightness} | COM={comDisplay} | 波特率={SelectedBaudRate}", showInStatusBar: true);
                    }
                }
                else
                {
                    Channel1Brightness = 0;
                    Channel2Brightness = 0;
                    Channel3Brightness = 0;
                    Channel4Brightness = 0;
                    SelectedBaudRate = SelectedLightType == LightType.Wordop ? 19200 : 9600;
                    LogInfo($"首次使用 {CurrentLightServiceName} 光源，使用默认配置", showInStatusBar: true);
                }
            }
            catch (Exception ex)
            {
                LogError($"加载配置失败: {ex.Message}");
                Channel1Brightness = 0;
                Channel2Brightness = 0;
                Channel3Brightness = 0;
                Channel4Brightness = 0;
                SelectedBaudRate = SelectedLightType == LightType.Wordop ? 19200 : 9600;
            }
        }

        #endregion

        #region 私有方法

        private void LoadComPorts()
        {
            try
            {
                ComPorts = new ObservableCollection<string>();
                var ports = System.IO.Ports.SerialPort.GetPortNames();

                var sortedPorts = ports.OrderBy(p =>
                {
                    if (p.StartsWith("COM") && int.TryParse(p.Substring(3), out int num))
                        return num;
                    return int.MaxValue;
                });

                foreach (var port in sortedPorts)
                {
                    ComPorts.Add(port);
                }

                if (ComPorts.Count > 0 && string.IsNullOrEmpty(SelectedComPort))
                {
                    SelectedComPort = ComPorts[0];
                }
            }
            catch (Exception ex)
            {
                LogError($"获取COM口失败: {ex.Message}");
                ComPorts = new ObservableCollection<string>();
            }

            BaudRates = new ObservableCollection<int> { 9600, 19200, 38400, 115200 };
            LightTypes = new ObservableCollection<LightType> { LightType.Wordop, LightType.Cognex };
        }

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        private async Task ConnectAsync()
        {
            if (_currentLightService == null)
            {
                LogError("当前光源服务未初始化", showInStatusBar: true);
                return;
            }

            if (IsConnected)
                return;

            if (string.IsNullOrWhiteSpace(SelectedComPort))
            {
                LogWarning("未选择COM端口，无法连接", showInStatusBar: true);
                return;
            }

            _isConnecting = true;
            RefreshCommands();

            try
            {
                LogInfo($"正在连接 {SelectedComPort} ({_currentLightService.ServiceName})...", showInStatusBar: true);

                bool success = await _currentLightService.ConnectAsync(SelectedComPort, SelectedBaudRate);
                IsConnected = success && _currentLightService.IsConnected;

                if (IsConnected)
                {
                    LogSuccess($"已成功连接到 {SelectedComPort} ({_currentLightService.ServiceName})", showInStatusBar: true);
                }
                else
                {
                    LogError($"连接失败：{SelectedComPort} ({_currentLightService.ServiceName})", showInStatusBar: true);
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                LogError($"连接异常: {ex.Message}", showInStatusBar: true);
            }
            finally
            {
                _isConnecting = false;
                RefreshCommands();
            }
        }

        private Task DisconnectAsync()
        {
            if (_currentLightService == null)
                return Task.CompletedTask;

            if (!IsConnected)
                return Task.CompletedTask;

            _isDisconnecting = true;
            RefreshCommands();

            try
            {
                LogInfo($"正在断开 ({_currentLightService.ServiceName})...", showInStatusBar: true);
                _currentLightService.Disconnect();
                IsConnected = _currentLightService.IsConnected;
                if (!IsConnected)
                {
                    LogSuccess("已断开连接", showInStatusBar: true);
                }
            }
            catch (Exception ex)
            {
                LogError($"断开异常: {ex.Message}", showInStatusBar: true);
            }
            finally
            {
                _isDisconnecting = false;
                RefreshCommands();
            }

            return Task.CompletedTask;
        }

        private async Task SetAllChannelsAsync(int brightness)
        {
            int limitedBrightness = Math.Min(Math.Max(brightness, 0), 255);

            try
            {
                LogInfo($"正在设置所有通道亮度为{limitedBrightness}...", showInStatusBar: true);
                Channel1Brightness = limitedBrightness;
                Channel2Brightness = limitedBrightness;
                Channel3Brightness = limitedBrightness;
                Channel4Brightness = limitedBrightness;

                if (IsConnected && _currentLightService != null)
                {
                    for (int ch = 1; ch <= 4; ch++)
                    {
                        await _currentLightService.SetBrightnessAsync(ch, limitedBrightness);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"批量设置异常: {ex.Message}");
            }
        }

        private async Task ReadAllChannelsAsync()
        {
            if (!IsConnected || _currentLightService == null)
            {
                ReadStatus = "未连接，无法读取";
                return;
            }

            try
            {
                ReadStatus = "正在读取所有通道...";

                for (int ch = 1; ch <= 4; ch++)
                {
                    int brightness = await _currentLightService.GetBrightnessAsync(ch);

                    switch (ch)
                    {
                        case 1: Channel1Brightness = brightness; break;
                        case 2: Channel2Brightness = brightness; break;
                        case 3: Channel3Brightness = brightness; break;
                        case 4: Channel4Brightness = brightness; break;
                    }
                }

                ReadStatus = "读取完成";
                System.Diagnostics.Debug.WriteLine($"读取所有通道完成 | CH1={Channel1Brightness} CH2={Channel2Brightness} CH3={Channel3Brightness} CH4={Channel4Brightness}");
            }
            catch (Exception ex)
            {
                ReadStatus = $"读取失败: {ex.Message}";
                LogError($"读取所有通道异常: {ex.Message}");
            }
        }

        private async Task ReadVersionAsync()
        {
            if (!IsConnected || _currentLightService == null)
            {
                VersionInfo = "未连接，无法读取";
                return;
            }

            try
            {
                VersionInfo = "正在读取版本号...";
                string version = await _currentLightService.ReadVersionAsync();
                VersionInfo = version;
                System.Diagnostics.Debug.WriteLine($"版本号: {version}");
            }
            catch (Exception ex)
            {
                VersionInfo = $"读取失败: {ex.Message}";
                LogError($"读取版本号异常: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
