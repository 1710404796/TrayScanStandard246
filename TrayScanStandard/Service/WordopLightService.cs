using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace TrayScanStandard.Services
{
    /// <summary>
    /// 沃德普(Wordop)光源控制器服务（仅ASCII模式）
    /// </summary>
    public class WordopLightService : ILightService
    {
        private const int MaxChannels = 4;
        private SerialPort _serialPort;
        private bool _isConnected;
        private readonly WordopProtocol _protocol;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public WordopLightService()
        {
            _protocol = new WordopProtocol();
        }

        public bool IsConnected => _isConnected;
        public string ServiceName => "Wordop沃德普";

        public async Task<bool> ConnectAsync(string comPort, int baudRate)
        {
            if (string.IsNullOrWhiteSpace(comPort) || baudRate <= 0)
                return false;

            await _connectionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isConnected && _serialPort != null && _serialPort.IsOpen)
                    return true;

                try
                {
                    _serialPort?.Close();
                }
                catch
                {
                    // ignore close errors
                }
                finally
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                }

                _serialPort = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\r\n",
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                _serialPort.Open();
                _isConnected = true;

                await Task.Delay(100).ConfigureAwait(false);
                return true;
            }
            catch
            {
                try
                {
                    _serialPort?.Close();
                }
                catch
                {
                    // ignore close errors
                }
                finally
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                }

                _isConnected = false;
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public void Disconnect()
        {
            _connectionLock.Wait();
            try
            {
                try
                {
                    _serialPort?.Close();
                }
                catch
                {
                    // ignore close errors
                }
                finally
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                }

                _isConnected = false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<bool> SetBrightnessAsync(int channel, int brightness)
        {
            if (!_isConnected || _serialPort == null) return false;

            try
            {
                byte[] command = _protocol.BuildSetBrightness(channel, brightness);
                Log.Information("[Light][Wordop] TX(SetBrightness CH{Channel}={Brightness}): {Cmd}", channel, brightness, Encoding.ASCII.GetString(command));
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Light][Wordop] SetBrightness failed. CH{Channel}={Brightness}", channel, brightness);
                return false;
            }
        }

        public async Task<int> GetBrightnessAsync(int channel)
        {
            if (!_isConnected || _serialPort == null) return -1;

            try
            {
                byte[] command = _protocol.BuildGetBrightness(channel);
                Log.Information("[Light][Wordop] TX(GetBrightness CH{Channel}): {Cmd}", channel, Encoding.ASCII.GetString(command));
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[1024];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                string raw = bytesRead > 0 ? Encoding.ASCII.GetString(buffer, 0, bytesRead) : string.Empty;
                int value = _protocol.ParseBrightnessResponse(buffer, bytesRead, channel);
                Log.Information("[Light][Wordop] RX(GetBrightness CH{Channel}): bytes={Bytes} raw={Raw} parsed={Value}", channel, bytesRead, raw.Replace("\r", "\\r").Replace("\n", "\\n"), value);
                return value;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Light][Wordop] GetBrightness failed. CH{Channel}", channel);
                return -1;
            }
        }

        public async Task<string> ReadParameterAsync()
        {
            if (!_isConnected || _serialPort == null) return "错误: 未连接";

            try
            {
                byte[] command = _protocol.BuildReadParameters();
                Log.Information("[Light][Wordop] TX(ReadParameters): {Cmd}", Encoding.ASCII.GetString(command));
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(100);

                byte[] buffer = new byte[4096];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                string raw = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                Log.Information("[Light][Wordop] RX(ReadParameters): bytes={Bytes} raw={Raw}", bytesRead, raw.Replace("\r", "\\r").Replace("\n", "\\n"));
                return raw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Light][Wordop] ReadParameters failed");
                return $"错误: {ex.Message}";
            }
        }

        public async Task<string> ReadVersionAsync()
        {
            if (!_isConnected || _serialPort == null) return "未知的版本信息";

            try
            {
                byte[] command = _protocol.BuildReadVersion();
                Log.Information("[Light][Wordop] TX(ReadVersion): {Cmd}", Encoding.ASCII.GetString(command));
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[1024];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                string raw = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                Log.Information("[Light][Wordop] RX(ReadVersion): bytes={Bytes} raw={Raw}", bytesRead, raw.Replace("\r", "\\r").Replace("\n", "\\n"));
                return raw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Light][Wordop] ReadVersion failed");
                return $"错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 按配置亮度统一下发 1-4 通道（用于拍照前亮灯）
        /// </summary>
        public async Task ApplyConfiguredBrightnessAsync()
        {
            var brightness = LoadConfiguredBrightness();
            for (int ch = 1; ch <= MaxChannels; ch++)
            {
                // 原逻辑（保留思路）：外部逐个 SetBrightnessAsync(ch, value)
                // 新逻辑：封装成服务内批量方法，减少调用侧重复代码
                await SetBrightnessAsync(ch, brightness[ch - 1]).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 统一关闭 1-4 通道（用于拍照后灭灯）
        /// </summary>
        public async Task TurnOffAllChannelsAsync()
        {
            for (int ch = 1; ch <= MaxChannels; ch++)
            {
                await SetBrightnessAsync(ch, 0).ConfigureAwait(false);
            }
        }

        private static int[] LoadConfiguredBrightness()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "WordopConfig.json");
                if (!File.Exists(configPath))
                {
                    return new[] { 255, 255, 255, 255 };
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<WordopConfig>(json);
                if (config == null)
                {
                    return new[] { 255, 255, 255, 255 };
                }

                return new[]
                {
                    Math.Clamp(config.Channel1Brightness, 0, 255),
                    Math.Clamp(config.Channel2Brightness, 0, 255),
                    Math.Clamp(config.Channel3Brightness, 0, 255),
                    Math.Clamp(config.Channel4Brightness, 0, 255)
                };
            }
            catch
            {
                return new[] { 255, 255, 255, 255 };
            }
        }

        private sealed class WordopConfig
        {
            public int Channel1Brightness { get; set; }
            public int Channel2Brightness { get; set; }
            public int Channel3Brightness { get; set; }
            public int Channel4Brightness { get; set; }
            public string LastComPort { get; set; } = string.Empty;
            public int LastBaudRate { get; set; }
        }
    }
}
