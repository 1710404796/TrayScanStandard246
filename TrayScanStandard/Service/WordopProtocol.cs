using System;
using System.Text;

namespace TrayScanStandard.Services
{
    /// <summary>
    /// 沃德普光源控制器通信协议（仅ASCII模式）
    /// </summary>
    public class WordopProtocol
    {
        /// <summary>
        /// 命令类型
        /// </summary>
        public enum CommandType
        {
            SetBrightness,      // 设置亮度
            GetBrightness,      // 读取亮度
            ReadVersion,        // 读取版本号
            ReadParameters      // 读取所有参数
        }

        /// <summary>
        /// 构建设置亮度指令
        /// 格式: CHx=yyy\r\n
        /// </summary>
        /// <param name="channel">通道 (1-4)</param>
        /// <param name="brightness">亮度 (0-255)</param>
        public byte[] BuildSetBrightness(int channel, int brightness)
        {
            string command = $"CH{channel}={brightness:D3}\r\n";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取亮度指令
        /// 格式: CHx?\r\n
        /// </summary>
        public byte[] BuildGetBrightness(int channel)
        {
            string command = $"CH{channel}?\r\n";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取版本号指令
        /// </summary>
        public byte[] BuildReadVersion()
        {
            string command = "VERSION\r\n";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取所有参数指令
        /// </summary>
        public byte[] BuildReadParameters()
        {
            string command = "READ\r\n";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 统一构建指令接口
        /// </summary>
        public byte[] BuildCommand(CommandType type, int channel = 1, int value = 0)
        {
            switch (type)
            {
                case CommandType.SetBrightness:
                    return BuildSetBrightness(channel, value);
                case CommandType.GetBrightness:
                    return BuildGetBrightness(channel);
                case CommandType.ReadVersion:
                    return BuildReadVersion();
                case CommandType.ReadParameters:
                    return BuildReadParameters();
                default:
                    throw new NotSupportedException($"不支持的命令: {type}");
            }
        }

        /// <summary>
        /// 解析响应数据，提取亮度值
        /// </summary>
        /// <param name="buffer">响应字节数组</param>
        /// <param name="bytesRead">实际读取的字节数</param>
        /// <param name="channel">请求的通道号</param>
        /// <returns>亮度值，失败返回-1</returns>
        public int ParseBrightnessResponse(byte[] buffer, int bytesRead, int channel)
        {
            if (buffer == null || bytesRead == 0)
                return -1;

            // 只取实际读取的部分
            string result = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

            // 匹配格式: CH1=128 或 CH1:128
            if (result.Contains($"CH{channel}=") || result.Contains($"CH{channel}:"))
            {
                string[] parts = result.Split('=', ':');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int brightness))
                {
                    return Math.Min(Math.Max(brightness, 0), 255);
                }
            }

            return -1;
        }

        /// <summary>
        /// 解析响应数据为字符串
        /// </summary>
        public string ParseResponseString(byte[] response)
        {
            if (response == null || response.Length == 0)
                return string.Empty;

            return Encoding.ASCII.GetString(response).Trim();
        }
    }
}