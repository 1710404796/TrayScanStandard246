using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TrayScanStandard.Services
{
    /// <summary>
    /// 沃德普(Wordop)光源控制器服务（仅ASCII模式）
    /// </summary>
    public class WordopLightService : ILightService
    {
        private SerialPort _serialPort;
        private bool _isConnected;
        private readonly WordopProtocol _protocol;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public WordopLightService()
        {
            _protocol = new WordopProtocol();
        }

        public bool IsConnected => _isConnected;
        public string ServiceName => "Wordop沃德普光源";

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
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> GetBrightnessAsync(int channel)
        {
            if (!_isConnected || _serialPort == null) return -1;

            try
            {
                byte[] command = _protocol.BuildGetBrightness(channel);
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[1024];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                return _protocol.ParseBrightnessResponse(buffer, bytesRead, channel);
            }
            catch
            {
                return -1;
            }
        }

        public async Task<string> ReadParameterAsync()
        {
            if (!_isConnected || _serialPort == null) return "错误: 未连接";

            try
            {
                byte[] command = _protocol.BuildReadParameters();
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(100);

                byte[] buffer = new byte[4096];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

                return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
            }
            catch (Exception ex)
            {
                return $"错误: {ex.Message}";
            }
        }

        public async Task<string> ReadVersionAsync()
        {
            if (!_isConnected || _serialPort == null) return "未知的版本信息";

            try
            {
                byte[] command = _protocol.BuildReadVersion();
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[1024];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);

                return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
            }
            catch (Exception ex)
            {
                return $"错误: {ex.Message}";
            }
        }
    }
}
