using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.Services
{
    /// <summary>
    /// 康耐视
    /// </summary>
    public class CognexLightService : ILightService
    {
        private SerialPort _serialPort;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string ServiceName => "Cognex康耐视";

        public async Task<bool> ConnectAsync(string comPort, int baudRate)
        {
            try
            {
                _serialPort = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One);
                _serialPort.Open();
                _isConnected = true;
                await Task.Delay(100);
                return true;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
            _isConnected = false;
        }

        public async Task<bool> SetBrightnessAsync(int channel, int brightness)
        {
            if (!_isConnected || _serialPort == null) return false;

            try
            {
                // Cognex 协议示例：不同格式，比如 "@CH1=128\r"
                string command = $"@{channel}={brightness}\r";
                byte[] data = Encoding.ASCII.GetBytes(command);
                await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
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
                string readCmd = $"?{channel}\r";
                byte[] cmdData = Encoding.ASCII.GetBytes(readCmd);
                await _serialPort.BaseStream.WriteAsync(cmdData, 0, cmdData.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[1024];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                var parts = response.Split('=');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int brightness))
                {
                    return Math.Min(Math.Max(brightness, 0), 255);
                }
                return -1;
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
                string paramCmd = "PARAM?\r";
                byte[] cmdData = Encoding.ASCII.GetBytes(paramCmd);
                await _serialPort.BaseStream.WriteAsync(cmdData, 0, cmdData.Length);

                await Task.Delay(50);

                byte[] buffer = new byte[2048];
                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                return response.Trim();
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
                // Cognex 协议示例：版本命令 "VER?\r"
                string versionCmd = "VER?\r";
                byte[] cmdData = Encoding.ASCII.GetBytes(versionCmd);
                await _serialPort.BaseStream.WriteAsync(cmdData, 0, cmdData.Length);

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